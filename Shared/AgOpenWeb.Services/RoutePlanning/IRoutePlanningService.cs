// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.RoutePlanning;
using AgOpenWeb.Services.Track;

namespace AgOpenWeb.Services.RoutePlanning;

/// <summary>
/// Generates a pre-computed coverage route for a field. Phase 1: a headland
/// margin + parallel swaths in boustrophedon order with turns at the ends.
/// See Plans/ROUTE_PLANNING_PHASE1.md.
/// </summary>
public interface IRoutePlanningService
{
    /// <summary>How the headland laps are emitted (classic laps, one continuous
    /// spiral in/out, or none). Operator's choice — set before Generate*.</summary>
    RouteHeadlandStyle HeadlandStyle { get; set; }

    /// <summary>True (default): headland laps drive before the interior fill.
    /// False: interior first, laps last (mow-style ordering — the headland
    /// stays clean for turning and is cut on the way out).</summary>
    bool HeadlandFirstPhase { get; set; }

    /// <summary>Finish the route with one extra fence-tight lap driven the
    /// opposite way round (mower back-cut).</summary>
    bool HeadlandBackCut { get; set; }

    /// <summary>
    /// Build a back-and-forth coverage route inside <paramref name="outerBoundary"/>
    /// (local-plane meters). Swaths are spaced by <paramref name="swathWidth"/>,
    /// turns sized to <paramref name="turnRadius"/>, with a
    /// <paramref name="headlandMargin"/> inset. Heading defaults to the longest
    /// boundary edge. <paramref name="pattern"/> selects adjacent back-and-forth
    /// or leap-frog (skip) ordering. Returns null if a route can't be built.
    /// </summary>
    RoutePlan? GenerateBoustrophedon(
        IReadOnlyList<Vec2> outerBoundary,
        double swathWidth,
        double turnRadius,
        double headlandMargin,
        double? headingRad = null,
        SwathPattern pattern = SwathPattern.Boustrophedon,
        int headlandPasses = 0,
        Vec3? startPos = null,
        double swathOffset = 0,
        bool swapEnds = false,
        bool startOppositeSide = false,
        double boundaryClearance = 0,
        int skipPasses = 0,
        int blockSkip = 0,
        double cornerRadius = 0,
        IReadOnlyList<IReadOnlyList<Vec2>>? innerBoundaries = null,
        bool addPondLoops = true,
        bool fastScore = false,
        double physicalToolWidth = 0,
        double passEndExtension = 0,
        IReadOnlyList<Vec2>? cultivatedOverride = null);

    /// <summary>
    /// Plan the field as operator-drawn regions: split lines carve the boundary
    /// into simpler shapes, each worked with its own (auto) pass direction while
    /// the headland laps still trace the whole true boundary. Returns null when
    /// the lines don't divide the field (caller falls back to plain planning).
    /// <paramref name="headingRad"/> forces every planned region to that pass
    /// direction (null = auto per region). <paramref name="onlyRegionAt"/> plans
    /// just the region containing that point (null when the pick misses).
    /// </summary>
    RoutePlan? GenerateSplitField(
        IReadOnlyList<Vec2> outerBoundary,
        IReadOnlyList<(Vec2 A, Vec2 B)> splitLines,
        double swathWidth,
        double turnRadius,
        double headlandMargin,
        SwathPattern pattern = SwathPattern.Boustrophedon,
        int headlandPasses = 0,
        Vec3? startPos = null,
        double boundaryClearance = 0,
        int skipPasses = 0,
        int blockSkip = 0,
        double cornerRadius = 0,
        IReadOnlyList<IReadOnlyList<Vec2>>? innerBoundaries = null,
        double physicalToolWidth = 0,
        double passEndExtension = 0,
        double? headingRad = null,
        Vec2? onlyRegionAt = null,
        int onlyRegionIndex = -1,
        IReadOnlyList<Vec2>? insetOverride = null);

    /// <summary>
    /// The split regions in stable "block label" order (north-most centroid
    /// first, then west-most): region 0 = block A, 1 = B, … Addressed by
    /// GenerateSplitField's onlyRegionIndex; stable while the lines don't change.
    /// </summary>
    List<List<Vec2>> ComputeSplitRegions(
        IReadOnlyList<Vec2> outerBoundary, IReadOnlyList<(Vec2 A, Vec2 B)> splitLines);

    /// <summary>
    /// Cross-drill: two complete coverages, the second rotated by
    /// <paramref name="crossAngleRad"/> from the first, joined end to end. For
    /// double ground coverage (e.g. seed drilling). Returns null if a route can't
    /// be built.
    /// </summary>
    RoutePlan? GenerateCrossDrill(
        IReadOnlyList<Vec2> outerBoundary,
        double swathWidth,
        double turnRadius,
        double headlandMargin,
        double headingRad,
        double crossAngleRad,
        SwathPattern pattern = SwathPattern.Boustrophedon,
        int headlandPasses = 0,
        Vec3? startPos = null,
        double swathOffset = 0,
        bool swapEnds = false,
        bool startOppositeSide = false,
        double boundaryClearance = 0,
        int skipPasses = 0,
        int blockSkip = 0,
        double cornerRadius = 0,
        IReadOnlyList<IReadOnlyList<Vec2>>? innerBoundaries = null,
        double rowSpacing = 0);

    /// <summary>
    /// Cross-drill driven as an interleaved weave: family A and B legs alternate
    /// with V-turns (the crossing angle, ~90°) in the side headlands — a W
    /// marching down the paddock with parallel return Ws — instead of two
    /// complete sequential coverages. Second-family legs and the offset second
    /// headland set are tagged coverage channel 1. Falls back to the sequential
    /// planner when the field has obstacles.
    /// </summary>
    RoutePlan? GenerateCrossDrillWoven(
        IReadOnlyList<Vec2> outerBoundary,
        double swathWidth,
        double turnRadius,
        double headlandMargin,
        double headingRad,
        double crossAngleRad,
        int headlandPasses = 0,
        Vec3? startPos = null,
        double boundaryClearance = 0,
        double cornerRadius = 0,
        IReadOnlyList<IReadOnlyList<Vec2>>? innerBoundaries = null,
        double passEndExtension = 0,
        double rowSpacing = 0,
        double entryRunIn = 0,
        bool trialHeadings = true);

    /// <summary>
    /// A simple spiral-in route: concentric inward laps at <paramref name="swathWidth"/>
    /// spacing from the boundary to the centre, joined into one continuous path.
    /// Honours the boundary clearance. Returns null if nothing fits.
    /// </summary>
    RoutePlan? GenerateSpiral(
        IReadOnlyList<Vec2> outerBoundary,
        double swathWidth,
        Vec3? startPos = null,
        double boundaryClearance = 0,
        double cornerRadius = 0,
        bool cornerLoops = false,
        IReadOnlyList<IReadOnlyList<Vec2>>? innerBoundaries = null);

    /// <summary>
    /// Uncovered regions of <paramref name="boundary"/> for an assembled
    /// <paramref name="plan"/>: field area minus the swept tool-width bands.
    /// Regions under <paramref name="minAreaM2"/> are dropped. Local-metre polygons.
    /// </summary>
    List<List<Vec2>> FindCoverageGaps(
        IReadOnlyList<Vec2> boundary, RoutePlan plan, double toolWidth, double minAreaM2);

    /// <summary>
    /// Like <see cref="GenerateBoustrophedon"/> but the passes follow a guide
    /// <paramref name="baseCurve"/> (e.g. a curved AB track), offset to both
    /// sides by whole swath widths and clipped to the cultivated polygon.
    /// </summary>
    RoutePlan? GenerateAlongCurve(
        IReadOnlyList<Vec2> outerBoundary,
        IReadOnlyList<Vec3> baseCurve,
        double swathWidth,
        double turnRadius,
        double headlandMargin,
        SwathPattern pattern = SwathPattern.Boustrophedon,
        int headlandPasses = 0,
        Vec3? startPos = null,
        bool swapEnds = false,
        bool startOppositeSide = false,
        double boundaryClearance = 0,
        int skipPasses = 0,
        int blockSkip = 0,
        double cornerRadius = 0);
}
