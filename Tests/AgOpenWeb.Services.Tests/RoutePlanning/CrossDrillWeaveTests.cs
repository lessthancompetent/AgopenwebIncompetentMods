// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.RoutePlanning;
using AgOpenWeb.Services.Geometry;
using AgOpenWeb.Services.RoutePlanning;

namespace AgOpenWeb.Services.Tests.RoutePlanning;

/// <summary>
/// Cross-drill upgrades: coverage-channel tagging of the second family, the
/// offset second headland set (half a row-unit spacing), and the woven W drive
/// order (alternating families with V-turns instead of two sequential
/// coverages with 180° keyholes).
/// </summary>
[TestFixture]
public class CrossDrillWeaveTests
{
    // 200 × 300 m rectangle, machine at the SW corner.
    private static readonly List<Vec2> Field = new()
    {
        new Vec2(0, 0), new Vec2(200, 0), new Vec2(200, 300), new Vec2(0, 300),
    };
    private static readonly Vec3 Start = new(-5, -5, 0);

    private const double Width = 3.0;
    private const double TurnR = 5.0;
    private const int Passes = 2;

    private static RoutePlanningService NewPlanner() => new(new PolygonOffsetService());

    [Test]
    public void Sequential_TagsSecondFamilyChannel1_AndAppendsOffsetHeadland()
    {
        var plan = NewPlanner().GenerateCrossDrill(Field, Width, TurnR, Passes * Width,
            headingRad: 0, crossAngleRad: Math.PI / 2,
            headlandPasses: Passes, startPos: Start, rowSpacing: 0.15);
        Assert.That(plan, Is.Not.Null);

        var swaths = plan!.Segments.Where(s => s.Type == RouteSegmentType.Swath).ToList();
        Assert.That(swaths.Any(s => s.Channel == 0), "first family on channel 0");
        Assert.That(swaths.Any(s => s.Channel == 1), "second family on channel 1");

        // The plan must END with the offset second headland set, channel 1.
        var tailLaps = new List<RouteSegment>();
        for (int i = plan.Segments.Count - 1; i >= 0; i--)
        {
            var s = plan.Segments[i];
            if (s.Type == RouteSegmentType.Headland) tailLaps.Add(s);
            else if (s.Type == RouteSegmentType.Swath) break;
        }
        Assert.That(tailLaps, Has.Count.EqualTo(Passes), "one offset lap per headland pass");
        Assert.That(tailLaps.All(s => s.Channel == 1), "second set works channel 1");

        // Offset check: the second set's outer lap sits half a row (7.5 cm)
        // further in than the first set's outer lap.
        double FirstLapMinDist(IEnumerable<RouteSegment> laps) =>
            laps.SelectMany(s => s.Points).Min(p => DistToBoundary(p));
        var headLaps = plan.Segments.Where(s => s.Type == RouteSegmentType.Headland && s.Channel == 0);
        double d0 = FirstLapMinDist(headLaps);
        double d1 = FirstLapMinDist(tailLaps);
        Assert.That(d1 - d0, Is.EqualTo(0.075).Within(0.03),
            "second headland set offset by half the row spacing");
    }

    [Test]
    public void Woven_DrivesEveryLegOnce_AlternatingFamilies()
    {
        var svc = NewPlanner();
        var sequential = svc.GenerateCrossDrill(Field, Width, TurnR, Passes * Width,
            headingRad: 0, crossAngleRad: Math.PI / 2, headlandPasses: Passes, startPos: Start);
        var woven = svc.GenerateCrossDrillWoven(Field, Width, TurnR, Passes * Width,
            headingRad: 0, crossAngleRad: Math.PI / 2, headlandPasses: Passes, startPos: Start);
        Assert.That(woven, Is.Not.Null);
        Assert.That(sequential, Is.Not.Null);

        var legs = woven!.Segments.Where(s => s.Type == RouteSegmentType.Swath).ToList();
        Assert.That(legs.Count, Is.EqualTo(
            sequential!.Segments.Count(s => s.Type == RouteSegmentType.Swath)),
            "the weave drives the same pass set, just in a different order");

        // Alternation: the weave switches family at (nearly) every leg — the
        // stragglers at the end may repeat a family, but the bulk alternates.
        int switches = 0;
        for (int i = 1; i < legs.Count; i++)
            if (legs[i].Channel != legs[i - 1].Channel) switches++;
        Assert.That(switches, Is.GreaterThan((int)(legs.Count * 0.75)),
            "A and B legs alternate through the weave (endgame stragglers excepted)");
    }

    [Test]
    public void Woven_TurnsAreVsNotKeyholes()
    {
        var svc = NewPlanner();
        var woven = svc.GenerateCrossDrillWoven(Field, Width, TurnR, Passes * Width,
            headingRad: 0, crossAngleRad: Math.PI / 2, headlandPasses: Passes, startPos: Start)!;

        // Direction change across each swath→swath junction: heading out of one
        // leg vs heading into the next. The weave's V is the crossing angle
        // (90°), where sequential adjacent passes need ~180°.
        var angles = new List<double>();
        RouteSegment? prev = null;
        foreach (var s in woven.Segments)
        {
            if (s.Type != RouteSegmentType.Swath) continue;
            if (prev != null && prev.Points.Count >= 2 && s.Points.Count >= 2)
            {
                double hOut = Heading(prev.Points[^2], prev.Points[^1]);
                double hIn = Heading(s.Points[0], s.Points[1]);
                double d = Math.Abs(hIn - hOut) % (2 * Math.PI);
                if (d > Math.PI) d = 2 * Math.PI - d;
                angles.Add(d);
            }
            prev = s;
        }
        Assert.That(angles, Is.Not.Empty);
        double meanDeg = angles.Average() * 180.0 / Math.PI;
        Assert.That(meanDeg, Is.LessThan(120), $"mean junction angle {meanDeg:F0}° should be V-like, not U-like");
        Assert.That(angles.Count(a => a < Math.PI * 0.75), Is.GreaterThan(angles.Count / 2),
            "most junctions are gentler than 135°");
    }

    [Test]
    public void Woven_WithObstacles_FallsBackToSequential()
    {
        var pond = new List<Vec2> { new(90, 140), new(110, 140), new(110, 160), new(90, 160) };
        var plan = NewPlanner().GenerateCrossDrillWoven(Field, Width, TurnR, Passes * Width,
            headingRad: 0, crossAngleRad: Math.PI / 2, headlandPasses: Passes, startPos: Start,
            innerBoundaries: new List<IReadOnlyList<Vec2>> { pond });
        Assert.That(plan, Is.Not.Null, "obstacle fields still plan (sequentially)");
        Assert.That(plan!.Segments.Any(s => s.Type == RouteSegmentType.Swath && s.Channel == 1),
            "fallback keeps the channel tagging");
    }

    private static double Heading(Vec3 a, Vec3 b) =>
        Math.Atan2(b.Easting - a.Easting, b.Northing - a.Northing);

    private static double DistToBoundary(Vec3 p)
    {
        double best = double.MaxValue;
        for (int i = 0; i < Field.Count; i++)
        {
            var a = Field[i];
            var b = Field[(i + 1) % Field.Count];
            double ex = b.Easting - a.Easting, ey = b.Northing - a.Northing;
            double len2 = ex * ex + ey * ey;
            double t = Math.Clamp(((p.Easting - a.Easting) * ex + (p.Northing - a.Northing) * ey) / len2, 0, 1);
            double dE = p.Easting - (a.Easting + t * ex), dN = p.Northing - (a.Northing + t * ey);
            best = Math.Min(best, Math.Sqrt(dE * dE + dN * dN));
        }
        return best;
    }
}
