// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using System.Linq;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.RoutePlanning;
using AgOpenWeb.Services.Geometry;
using AgOpenWeb.Services.RoutePlanning;

namespace AgOpenWeb.Services.Tests.RoutePlanning;

/// <summary>
/// Operator-selectable headland styles (Route Planner → Headland tab):
/// classic separate laps, one continuous spiral in/out, none at all, the
/// headland-first/-last ordering, and the mower back-cut lap.
/// </summary>
[TestFixture]
public class HeadlandStyleTests
{
    // 200 × 100 m rectangle, machine parked just outside the SW corner.
    private static readonly List<Vec2> Field = new()
    {
        new Vec2(0, 0), new Vec2(200, 0), new Vec2(200, 100), new Vec2(0, 100),
    };
    private static readonly Vec3 Start = new(-5, -5, 0);

    private const double Width = 6.0;
    private const double TurnR = 5.0;
    private const int Passes = 2;

    private static RoutePlanningService NewPlanner() => new(new PolygonOffsetService());

    private static RoutePlan Plan(RoutePlanningService svc)
    {
        var plan = svc.GenerateBoustrophedon(Field, Width, TurnR, Passes * Width,
            headingRad: 0, headlandPasses: Passes, startPos: Start);
        Assert.That(plan, Is.Not.Null);
        return plan!;
    }

    private static List<RouteSegment> Laps(RoutePlan plan) =>
        plan.Segments.Where(s => s.Type == RouteSegmentType.Headland).ToList();

    private static double PathLen(IReadOnlyList<Vec3> pts)
    {
        double d = 0;
        for (int i = 1; i < pts.Count; i++)
            d += GeometryMath.Distance(pts[i - 1], pts[i]);
        return d;
    }

    /// <summary>Shoelace signed area of a (near-)closed path — the SIGN gives
    /// the traversal direction, which is what the back-cut flips.</summary>
    private static double SignedArea(IReadOnlyList<Vec3> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            a += p.Easting * q.Northing - q.Easting * p.Northing;
        }
        return a / 2.0;
    }

    private static double MinDistToBoundary(Vec3 p)
    {
        double best = double.MaxValue;
        for (int i = 0; i < Field.Count; i++)
        {
            var a = Field[i];
            var b = Field[(i + 1) % Field.Count];
            double ex = b.Easting - a.Easting, ey = b.Northing - a.Northing;
            double len2 = ex * ex + ey * ey;
            double t = ((p.Easting - a.Easting) * ex + (p.Northing - a.Northing) * ey) / len2;
            t = System.Math.Clamp(t, 0, 1);
            double dE = p.Easting - (a.Easting + t * ex), dN = p.Northing - (a.Northing + t * ey);
            best = System.Math.Min(best, System.Math.Sqrt(dE * dE + dN * dN));
        }
        return best;
    }

    private static int FirstIndex(RoutePlan plan, RouteSegmentType t)
    {
        for (int i = 0; i < plan.Segments.Count; i++)
            if (plan.Segments[i].Type == t) return i;
        return -1;
    }

    private static int LastIndex(RoutePlan plan, RouteSegmentType t)
    {
        for (int i = plan.Segments.Count - 1; i >= 0; i--)
            if (plan.Segments[i].Type == t) return i;
        return -1;
    }

    [Test]
    public void DefaultStyle_IsClassicLapsBeforeInterior()
    {
        var plan = Plan(NewPlanner());
        Assert.That(Laps(plan), Has.Count.EqualTo(Passes), "one Headland segment per lap");
        Assert.That(FirstIndex(plan, RouteSegmentType.Headland),
            Is.LessThan(FirstIndex(plan, RouteSegmentType.Swath)), "laps drive first");
    }

    [Test]
    public void SpiralIn_IsOneContinuousPathCoveringEveryLap()
    {
        var classic = Plan(NewPlanner());
        double classicLen = Laps(classic).Sum(s => PathLen(s.Points));

        var svc = NewPlanner();
        svc.HeadlandStyle = RouteHeadlandStyle.SpiralIn;
        var plan = Plan(svc);
        var laps = Laps(plan);

        Assert.That(laps, Has.Count.EqualTo(1), "the spiral is ONE unbroken path");
        Assert.That(PathLen(laps[0].Points), Is.GreaterThan(classicLen * 0.95),
            "each lap still winds its full ring (plus the lane changes)");
        Assert.That(FirstIndex(plan, RouteSegmentType.Headland),
            Is.LessThan(FirstIndex(plan, RouteSegmentType.Swath)));
        foreach (var p in laps[0].Points)
            Assert.That(MinDistToBoundary(p), Is.GreaterThan(Width * 0.25),
                "the spiral stays inside the fence");
        // Spiral-in finishes on the INNERMOST lap, ready to link to the fill.
        Assert.That(MinDistToBoundary(laps[0].Points[^1]), Is.GreaterThan(Width),
            "spiral-in ends on the inner lap");
    }

    [Test]
    public void SpiralOut_WithHeadlandLast_FinishesOnTheFenceLap()
    {
        var svc = NewPlanner();
        svc.HeadlandStyle = RouteHeadlandStyle.SpiralOut;
        svc.HeadlandFirstPhase = false;
        var plan = Plan(svc);
        var laps = Laps(plan);

        Assert.That(laps, Has.Count.EqualTo(1));
        Assert.That(FirstIndex(plan, RouteSegmentType.Headland),
            Is.GreaterThan(LastIndex(plan, RouteSegmentType.Swath)),
            "headland-last: the interior fill drives first");
        Assert.That(MinDistToBoundary(laps[0].Points[^1]), Is.LessThan(Width),
            "spiral-out ends on the fence lap (at the gate)");
    }

    [Test]
    public void StyleNone_KeepsTheMarginButDrivesNoLaps()
    {
        var classic = Plan(NewPlanner());
        var svc = NewPlanner();
        svc.HeadlandStyle = RouteHeadlandStyle.None;
        var plan = Plan(svc);

        Assert.That(Laps(plan), Is.Empty, "no headland laps in the route");
        Assert.That(plan.Metadata.SwathCount, Is.EqualTo(classic.Metadata.SwathCount),
            "the interior fill is unchanged — the margin is still reserved");
    }

    [Test]
    public void SkipOuterLaps_DropsWorkedLapsKeepsInteriorAndDepth()
    {
        var full = Plan(NewPlanner());
        var svc = NewPlanner();
        svc.HeadlandSkipOuterLaps = 1;   // e.g. the boundary-recording lap, already worked
        var plan = Plan(svc);

        Assert.That(Laps(plan), Has.Count.EqualTo(Passes - 1), "outermost lap left out");
        Assert.That(plan.Metadata.SwathCount, Is.EqualTo(full.Metadata.SwathCount),
            "interior fill unchanged — the band is still reserved");
        // The remaining lap is the ORIGINAL second ring, not a re-spaced one.
        double d = Laps(plan)[0].Points.Min(p => MinDistToBoundary(p));
        Assert.That(d, Is.GreaterThan(Width), "remaining lap keeps its original inset");
    }

    [Test]
    public void SlopeCorrectedSpacing_TightensPassCombOnSideSlopes()
    {
        // Synthetic terrain: constant 30% cross-slope along E. Passes run north
        // (heading 0), so E is the spacing axis: expected map spacing =
        // width · cos(atan 0.3) = width / sqrt(1.09) ≈ 0.958 · width.
        var flat = Plan(NewPlanner());
        var svc = NewPlanner();
        svc.ElevationSampler = (e, n) => 0.3 * e;
        var sloped = Plan(svc);

        Assert.That(sloped.Metadata.SwathCount, Is.GreaterThan(flat.Metadata.SwathCount),
            "tighter ground spacing needs more passes to cover the same field");

        var swaths = sloped.Segments.Where(s => s.Type == RouteSegmentType.Swath).ToList();
        double gap = Math.Abs(swaths[1].Points[0].Easting - swaths[0].Points[0].Easting);
        Assert.That(gap, Is.EqualTo(Width / Math.Sqrt(1.09)).Within(0.05),
            "consecutive passes sit width·cos(cross-slope) apart on the map");
    }

    [Test]
    public void BackCut_AppendsOneOppositeHandFenceLapAtTheEnd()
    {
        var svc = NewPlanner();
        svc.HeadlandBackCut = true;
        var plan = Plan(svc);
        var laps = Laps(plan);

        Assert.That(laps, Has.Count.EqualTo(Passes + 1), "the extra back-cut lap");
        Assert.That(plan.Segments[^1].Type, Is.EqualTo(RouteSegmentType.Headland),
            "the back-cut is the very last thing driven");
        double firstLapArea = SignedArea(laps[0].Points);
        double backCutArea = SignedArea(plan.Segments[^1].Points);
        Assert.That(firstLapArea * backCutArea, Is.LessThan(0),
            "the back-cut runs the opposite way round");
        Assert.That(MinDistToBoundary(plan.Segments[^1].Points[0]), Is.LessThan(Width),
            "the back-cut hugs the fence");
    }
}
