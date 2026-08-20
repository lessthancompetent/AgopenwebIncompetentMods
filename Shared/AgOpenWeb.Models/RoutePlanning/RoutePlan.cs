// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using AgOpenWeb.Models.Base;

namespace AgOpenWeb.Models.RoutePlanning;

/// <summary>Kind of route segment.</summary>
public enum RouteSegmentType
{
    /// <summary>A worked interior pass.</summary>
    Swath,
    /// <summary>A U-turn linking two passes at the headland.</summary>
    Turn,
    /// <summary>A perimeter headland lap driven around the field edge.</summary>
    Headland,
    /// <summary>A non-working transport move — drive-to-start / lap connector.</summary>
    Approach
}

/// <summary>How the headland laps are driven. Operator's choice, to suit the
/// tool, field and job — see the Route Planner's Headland tab.</summary>
public enum RouteHeadlandStyle
{
    /// <summary>Separate closed laps, outermost first (classic).</summary>
    Laps = 0,
    /// <summary>One continuous spiral from the fence inward: each lap closes at
    /// its own start then lane-changes one width in onto the next lap.</summary>
    SpiralIn = 1,
    /// <summary>The same spiral driven inside-out, finishing on the fence lap —
    /// pairs with headland-last so the route ends at the field entry.</summary>
    SpiralOut = 2,
    /// <summary>No headland laps in the route (the margin is still reserved for
    /// turns) — for working the headland separately or by hand.</summary>
    None = 3,
}

/// <summary>
/// One leg of a route: a worked swath or the turn that links two swaths.
/// Points are in the field's local plane (meters, with heading).
/// </summary>
public sealed class RouteSegment
{
    public RouteSegment(RouteSegmentType type, IReadOnlyList<Vec3> points, int channel = 0)
    {
        Type = type;
        Points = points;
        Channel = channel;
    }

    public RouteSegmentType Type { get; }
    public IReadOnlyList<Vec3> Points { get; }

    /// <summary>
    /// Coverage channel this segment works (cross-drilling): 0 = first pass
    /// family, 1 = the deliberate second coverage (crossing family / offset
    /// second headland set). Drives ICoverageMapService.ActiveChannel while
    /// the segment is worked, so the two families' paint and "already
    /// covered" checks stay independent.
    /// </summary>
    public int Channel { get; }
}

/// <summary>Summary stats for a route, for display and quick comparison.</summary>
public sealed class RoutePlanMetadata
{
    public RoutePlanMetadata(int swathCount, double totalDistanceMeters, double estimatedSeconds,
        double workDistanceMeters = 0, double turnDistanceMeters = 0, int turnCount = 0,
        double toolWidthMeters = 0)
    {
        SwathCount = swathCount;
        TotalDistanceMeters = totalDistanceMeters;
        EstimatedSeconds = estimatedSeconds;
        WorkDistanceMeters = workDistanceMeters;
        TurnDistanceMeters = turnDistanceMeters;
        TurnCount = turnCount;
        ToolWidthMeters = toolWidthMeters;
    }

    public int SwathCount { get; }
    public double TotalDistanceMeters { get; }
    public double EstimatedSeconds { get; }

    /// <summary>Distance with the tool working (swaths + headland laps).</summary>
    public double WorkDistanceMeters { get; }
    /// <summary>Distance with the tool lifted (turns + transport/approach moves).</summary>
    public double TurnDistanceMeters { get; }
    /// <summary>Number of headland U-turns linking passes.</summary>
    public int TurnCount { get; }
    /// <summary>Implement width — for swept-area and coverage rendering.</summary>
    public double ToolWidthMeters { get; }
}

/// <summary>
/// An immutable pre-computed coverage route: an ordered sequence of swath +
/// turn segments covering a field, plus summary metadata. Produced by the
/// route-planning service; consumed by the map preview (and, later, guidance).
/// See Plans/ROUTE_PLANNING_PHASE1.md.
/// </summary>
public sealed class RoutePlan
{
    public RoutePlan(IReadOnlyList<RouteSegment> segments, RoutePlanMetadata metadata)
    {
        Segments = segments;
        Metadata = metadata;
    }

    public IReadOnlyList<RouteSegment> Segments { get; }
    public RoutePlanMetadata Metadata { get; }

    /// <summary>
    /// Uncovered regions inside the field (field area minus the swept tool-width
    /// bands) — the "missed spots". Computed on demand for the coverage preview;
    /// empty unless filled in. Each entry is a closed polygon in local meters.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Vec2>> MissedRegions { get; set; }
        = System.Array.Empty<IReadOnlyList<Vec2>>();
}
