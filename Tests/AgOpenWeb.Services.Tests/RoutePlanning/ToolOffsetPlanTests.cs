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
using AgOpenWeb.Services.Track;

namespace AgOpenWeb.Services.Tests.RoutePlanning;

/// <summary>
/// Pins the planner half of the lateral tool-offset model: combs are planned
/// as TOOL centerlines; at assembly each drive leg is shifted left-of-travel
/// by the offset, so drive lines alternate w∓2o spacing while the tool bands
/// (drive line + o·rightOfTravel) tile at exactly w. Mirrors the live
/// guidance model in GuidanceGeometry (sim-verified 47.2/56.8/63.2 laterals
/// at w=8, o=0.8).
/// </summary>
[TestFixture]
public class ToolOffsetPlanTests
{
    // 200 × 100 m rectangle; passes run east-west (heading 90°) so northing
    // is the lateral axis of the interior combs.
    private static readonly List<Vec2> Field = new()
    {
        new Vec2(0, 0), new Vec2(200, 0), new Vec2(200, 100), new Vec2(0, 100),
    };
    private static readonly Vec3 Start = new(-5, -5, 0);

    private const double Width = 6.0;
    private const double TurnR = 4.0;
    private const double Offset = 0.8;

    private static RoutePlanningService NewPlanner() => new(new PolygonOffsetService());

    private static List<RouteSegment> Swaths(RoutePlan plan) =>
        plan.Segments.Where(s => s.Type == RouteSegmentType.Swath).ToList();

    // Travel direction of an east-west leg: +1 driving east, −1 driving west.
    private static int DirEast(RouteSegment s) =>
        Math.Sign(s.Points[^1].Easting - s.Points[0].Easting);

    private static double MidNorthing(RouteSegment s) =>
        s.Points[s.Points.Count / 2].Northing;

    [Test]
    public void OffsetPlan_DriveLinesAlternate_ToolBandsTileAtWidth()
    {
        var svc = NewPlanner();
        svc.ToolOffset = Offset;
        var plan = svc.GenerateBoustrophedon(Field, Width, TurnR, 2 * Width,
            headingRad: Math.PI / 2, headlandPasses: 0, startPos: Start);
        Assert.That(plan, Is.Not.Null);

        var swaths = Swaths(plan!);
        Assert.That(swaths.Count, Is.GreaterThan(4), "should plan several passes");

        // For each drive leg: the tool band = drive line + o·rightOfTravel.
        // Heading 90° (east): right of travel = SOUTH (−northing). Driving
        // west: right of travel = NORTH (+northing).
        var bands = new List<double>();
        for (int i = 0; i < swaths.Count; i++)
        {
            int dir = DirEast(swaths[i]);
            Assert.That(dir, Is.Not.Zero, "east-west leg expected");
            double band = MidNorthing(swaths[i]) + (dir > 0 ? -Offset : +Offset);
            bands.Add(band);
        }

        // Tool bands must tile at EXACTLY the working width (seam-tight).
        var sorted = bands.OrderBy(b => b).ToList();
        for (int i = 1; i < sorted.Count; i++)
            Assert.That(sorted[i] - sorted[i - 1], Is.EqualTo(Width).Within(0.05),
                $"tool bands must tile at w={Width} (gap {i})");

        // Adjacent passes in drive order alternate direction, so consecutive
        // DRIVE lines alternate w−2o / w+2o spacing.
        var spacings = new List<double>();
        for (int i = 1; i < swaths.Count; i++)
        {
            if (DirEast(swaths[i]) == DirEast(swaths[i - 1])) continue; // skip-order jumps
            if (Math.Abs(MidNorthing(swaths[i]) - MidNorthing(swaths[i - 1])) > Width * 1.5) continue;
            spacings.Add(Math.Abs(MidNorthing(swaths[i]) - MidNorthing(swaths[i - 1])));
        }
        Assert.That(spacings, Is.Not.Empty, "need at least one adjacent opposite-direction pair");
        foreach (var sp in spacings)
            Assert.That(Math.Min(Math.Abs(sp - (Width - 2 * Offset)), Math.Abs(sp - (Width + 2 * Offset))),
                Is.LessThan(0.05),
                $"drive-line spacing {sp:F2} must be w−2o={Width - 2 * Offset} or w+2o={Width + 2 * Offset}");
    }

    [Test]
    public void ZeroOffset_PlanUnchanged_DriveLinesOnCombs()
    {
        var svc = NewPlanner();
        svc.ToolOffset = 0;
        var plan = svc.GenerateBoustrophedon(Field, Width, TurnR, 2 * Width,
            headingRad: Math.PI / 2, headlandPasses: 0, startPos: Start);
        Assert.That(plan, Is.Not.Null);

        var laterals = Swaths(plan!).Select(MidNorthing).OrderBy(n => n).ToList();
        for (int i = 1; i < laterals.Count; i++)
            Assert.That(laterals[i] - laterals[i - 1], Is.EqualTo(Width).Within(0.05));
    }

    [Test]
    public void TightSpacing_DrivesRecommendations()
    {
        // w=6, o=1.3: tight spacing 3.4 — a 2.5 m-radius machine (diameter 5)
        // no longer flips into the adjacent pass.
        Assert.That(RoutePlanningService.RecommendPattern(6, 2.5, 0), Is.EqualTo(SwathPattern.Boustrophedon));
        Assert.That(RoutePlanningService.RecommendPattern(6, 2.5, 1.3), Is.EqualTo(SwathPattern.Snake));
        Assert.That(RoutePlanningService.RecommendHeadlandPasses(6, 5, 1.3),
            Is.GreaterThanOrEqualTo(RoutePlanningService.RecommendHeadlandPasses(6, 5, 0)));
        Assert.That(RoutePlanningService.RecommendHeadlandWidth(6, 5, 1.3),
            Is.EqualTo(RoutePlanningService.RecommendHeadlandWidth(6, 5, 0) + 1.3).Within(1e-9));
    }
}
