// AgOpenWeb
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Field splitting: operator-drawn split lines carve the boundary into simpler
// regions, each planned with its own pass direction, headland laps still on the
// whole true boundary. These tests pin the splitter geometry and the composed
// plan on the canonical case — an L-shaped field cut into two rectangles.

using System;
using System.Collections.Generic;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.RoutePlanning;
using AgOpenWeb.Services.Geometry;
using AgOpenWeb.Services.RoutePlanning;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class SplitFieldPlanningTests
{
    // L-shape: 100 m wide arm along the bottom (300 x 100), 100 m wide arm up the
    // left (100 x 300). A vertical split at E=100 separates them into a tall
    // rectangle and a wide one — the "triangle and a square" working style.
    private static readonly List<Vec2> LShape = new()
    {
        new(0, 0), new(300, 0), new(300, 100), new(100, 100), new(100, 300), new(0, 300),
    };

    private static readonly (Vec2 A, Vec2 B)[] VerticalSplit =
    {
        (new Vec2(100, 90), new Vec2(100, 110)),   // partial stroke — must extend
    };

    [Test]
    public void Splitter_cuts_L_into_two_regions_preserving_area()
    {
        var regions = RoutePlanningService.SplitPolygon(LShape, VerticalSplit);
        Assert.That(regions.Count, Is.EqualTo(2), "one vertical line should yield two regions");

        double total = 0;
        foreach (var r in regions) total += Math.Abs(Area(r));
        // L area = 300*100 + 100*200 = 50,000 m²
        Assert.That(total, Is.EqualTo(50_000).Within(50), "split must not lose or invent area");
    }

    [Test]
    public void Splitter_line_missing_the_field_changes_nothing()
    {
        var miss = new[] { (new Vec2(-50, -50), new Vec2(-50, -40)) };
        var regions = RoutePlanningService.SplitPolygon(LShape, miss);
        Assert.That(regions.Count, Is.EqualTo(1));
        Assert.That(Math.Abs(Area(regions[0])), Is.EqualTo(50_000).Within(50));
    }

    [Test]
    public void SplitField_plans_both_regions_with_their_own_headings()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        var plan = svc.GenerateSplitField(LShape, VerticalSplit,
            swathWidth: 6, turnRadius: 6, headlandMargin: 12, headlandPasses: 1,
            startPos: new Vec3(5, 5, 0));

        Assert.That(plan, Is.Not.Null, "split plan should generate");
        Assert.That(plan!.Segments.Count, Is.GreaterThan(4));

        // Work must land on BOTH sides of the split: points well inside the left
        // arm (E<95) and well inside the bottom-right arm (E>110).
        bool left = false, right = false;
        foreach (var seg in plan.Segments)
            foreach (var p in seg.Points)
            {
                if (p.Easting < 95 && p.Northing > 120) left = true;
                if (p.Easting > 110 && p.Northing < 90) right = true;
            }
        Assert.That(left, Is.True, "left region should contain passes");
        Assert.That(right, Is.True, "right region should contain passes");

        // Regions get their OWN headings: the tall arm wants ~north-south passes,
        // the wide arm ~east-west. Look at long straight runs on each side.
        Assert.That(DominantAxisIsNorthSouth(plan, eMax: 95, nMin: 120), Is.True,
            "tall left arm should be worked roughly north-south");
        Assert.That(DominantAxisIsNorthSouth(plan, eMin: 110, nMax: 90), Is.False,
            "wide bottom arm should be worked roughly east-west");
    }

    [Test]
    public void SplitField_block_pick_plans_only_that_region_without_laps()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        var plan = svc.GenerateSplitField(LShape, VerticalSplit,
            swathWidth: 6, turnRadius: 6, headlandMargin: 12, headlandPasses: 0,
            startPos: new Vec3(5, 5, 0), onlyRegionAt: new Vec2(50, 200)); // inside the tall left arm

        Assert.That(plan, Is.Not.Null, "picking inside a block should plan it");
        bool leftWork = false;
        foreach (var seg in plan!.Segments)
        {
            Assert.That(seg.Type, Is.Not.EqualTo(RouteSegmentType.Headland),
                "single-block plan must not include headland laps");
            if (seg.Type != RouteSegmentType.Swath) continue;
            foreach (var p in seg.Points)
            {
                Assert.That(p.Easting > 110 && p.Northing < 90, Is.False,
                    "no work may land in the unpicked bottom-right arm");
                if (p.Easting < 95 && p.Northing > 120) leftWork = true;
            }
        }
        Assert.That(leftWork, Is.True, "the picked left arm should contain passes");
    }

    [Test]
    public void ComputeSplitRegions_orders_blocks_north_first_for_stable_labels()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        var regions = svc.ComputeSplitRegions(LShape, VerticalSplit);
        Assert.That(regions.Count, Is.EqualTo(2));
        // Block A (index 0) = tall left arm (north-most centroid); B = bottom-right arm.
        double cn0 = 0, cn1 = 0;
        foreach (var p in regions[0]) cn0 += p.Northing; cn0 /= regions[0].Count;
        foreach (var p in regions[1]) cn1 += p.Northing; cn1 /= regions[1].Count;
        Assert.That(cn0, Is.GreaterThan(cn1), "A must be the northern block");
    }

    [Test]
    public void SplitField_region_index_addresses_the_labelled_block()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        // Index 1 = block B = the bottom-right arm; no work may land in the tall arm.
        var plan = svc.GenerateSplitField(LShape, VerticalSplit,
            swathWidth: 6, turnRadius: 6, headlandMargin: 12, headlandPasses: 0,
            onlyRegionIndex: 1);
        Assert.That(plan, Is.Not.Null);
        bool rightWork = false;
        foreach (var seg in plan!.Segments)
        {
            if (seg.Type != RouteSegmentType.Swath) continue;
            foreach (var p in seg.Points)
            {
                Assert.That(p.Easting < 95 && p.Northing > 120, Is.False,
                    "index 1 must not work the tall left arm");
                if (p.Easting > 110 && p.Northing < 90) rightWork = true;
            }
        }
        Assert.That(rightWork, Is.True);
    }

    [Test]
    public void SplitField_pick_outside_every_region_returns_null()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        var plan = svc.GenerateSplitField(LShape, VerticalSplit,
            swathWidth: 6, turnRadius: 6, headlandMargin: 12,
            onlyRegionAt: new Vec2(250, 250)); // in the L's notch — outside the field
        Assert.That(plan, Is.Null);
    }

    [Test]
    public void SplitField_manual_heading_overrides_every_region()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        // Force east-west passes (heading π/2) — even the tall arm, whose auto
        // choice is north-south, must follow the operator's angle.
        var plan = svc.GenerateSplitField(LShape, VerticalSplit,
            swathWidth: 6, turnRadius: 6, headlandMargin: 12, headlandPasses: 1,
            startPos: new Vec3(5, 5, 0), headingRad: Math.PI / 2.0);

        Assert.That(plan, Is.Not.Null);
        Assert.That(DominantAxisIsNorthSouth(plan!, eMax: 95, nMin: 120), Is.False,
            "manual angle must override the tall arm's auto north-south heading");
        Assert.That(DominantAxisIsNorthSouth(plan!, eMin: 110, nMax: 90), Is.False,
            "wide arm follows the manual east-west heading too");
    }

    [Test]
    public void SplitField_returns_null_when_line_does_not_divide()
    {
        var svc = new RoutePlanningService(new PolygonOffsetService());
        var miss = new[] { (new Vec2(-50, -50), new Vec2(-50, -40)) };
        var plan = svc.GenerateSplitField(LShape, miss,
            swathWidth: 6, turnRadius: 6, headlandMargin: 12);
        Assert.That(plan, Is.Null);
    }

    // ---- helpers ----

    private static double Area(IReadOnlyList<Vec2> poly)
    {
        double a = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % poly.Count];
            a += p.Easting * q.Northing - q.Easting * p.Northing;
        }
        return a / 2.0;
    }

    /// <summary>True when the summed segment travel inside the given window is
    /// predominantly north-south rather than east-west.</summary>
    private static bool DominantAxisIsNorthSouth(RoutePlan plan,
        double eMin = double.MinValue, double eMax = double.MaxValue,
        double nMin = double.MinValue, double nMax = double.MaxValue)
    {
        double ns = 0, ew = 0;
        foreach (var seg in plan.Segments)
            for (int i = 1; i < seg.Points.Count; i++)
            {
                var p = seg.Points[i - 1];
                var q = seg.Points[i];
                if (p.Easting < eMin || p.Easting > eMax || p.Northing < nMin || p.Northing > nMax) continue;
                ns += Math.Abs(q.Northing - p.Northing);
                ew += Math.Abs(q.Easting - p.Easting);
            }
        return ns > ew;
    }
}
