// AgOpenWeb — headless route-planning API (Layer 2).
//
// Ported from the AgValoniaGPS-RoutePlanner fork's PlanRouteJob, trimmed to a
// web-first headless surface: the route options the fork read from DisplayConfig
// (pattern / headland passes / skip / block / angle) come in as explicit args
// from the web command; everything else (tool width, turn radius, clearance,
// boundary, vehicle pose) is read from AgOpenWeb's existing config + state.
//
// Web → PlanRoute(...) builds a RoutePlan and stashes it; the web then GETs
// /api/routeplan (GetRoutePlanJson) and draws the polylines client-side, the
// same lightweight pattern used for pick-from-map. No binary scene-protocol change.
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.RoutePlanning;
using AgOpenWeb.Models.Track;
using AgOpenWeb.Services.Geometry;
using AgOpenWeb.Services.RoutePlanning;
using AgOpenWeb.Services.Track;

namespace AgOpenWeb.ViewModels;

public partial class MainViewModel
{
    private IRoutePlanningService? _routePlannerBacking;
    private IRoutePlanningService RoutePlanner =>
        _routePlannerBacking ??= new RoutePlanningService(new PolygonOffsetService());

    /// <summary>The most recently planned coverage route, for the web preview.</summary>
    private RoutePlan? _currentRoutePlan;

    /// <summary>When driving a planned route, halt the sim at the path end (the
    /// record/playback feature leaves the user in control of speed, so this is gated).</summary>
    private bool _haltSimAtRouteEnd;

    /// <summary>
    /// Plan a coverage route for the open field and stash it for the web preview.
    /// Pattern: 0 auto, 1 skip, 2 cross-drill, 3 spiral, 4 block.
    /// headlandPasses 0 = auto (route-planner rule); skipCount/blockSkip used by
    /// the skip/cross/block patterns; angleDeg rotates the field-aligned passes.
    /// </summary>
    public void PlanRoute(int pattern, int headlandPasses, int skipCount, int blockSkip, double angleDeg, bool cornerFill = false)
    {
        if (State.Field.ActiveField?.Boundary?.OuterBoundary is not { IsValid: true } outer)
        {
            StatusMessage = "Open a field with a boundary to plan a route";
            _currentRoutePlan = null;
            return;
        }

        var pts = new List<Vec2>(outer.Points.Count);
        foreach (var p in outer.Points) pts.Add(new Vec2(p.Easting, p.Northing));

        // Inner obstacles the route must avoid: only HARD inner boundaries (a
        // drive-through / soft exclusion is driven over — sections just switch off —
        // so it isn't routed around). Mirrors Boundary.IsPointInside, which ignores
        // drive-through inners.
        List<IReadOnlyList<Vec2>>? inners = null;
        var innerList = State.Field.ActiveField?.Boundary?.InnerBoundaries;
        if (innerList is { Count: > 0 })
        {
            inners = new List<IReadOnlyList<Vec2>>();
            foreach (var ib in innerList)
            {
                if (ib is not { IsValid: true } || ib.IsDriveThrough) continue;
                var ring = new List<Vec2>(ib.Points.Count);
                foreach (var p in ib.Points) ring.Add(new Vec2(p.Easting, p.Northing));
                if (ring.Count >= 3) inners.Add(ring);
            }
            if (inners.Count == 0) inners = null;
        }

        double width = _configStore.ActualToolWidth;
        if (width <= 0.1) width = 6.0;
        double turnRadius = _configStore.Guidance.UTurnRadius;
        if (turnRadius <= 0.1) turnRadius = width / 2.0;

        double minTurn = _configStore.Vehicle.MinTurningRadius;
        if (double.IsNaN(minTurn) || double.IsInfinity(minTurn) || minTurn <= 0.1)
            minTurn = turnRadius;
        turnRadius = Math.Max(turnRadius, minTurn);

        // Headland width + lap count. Auto (0) uses the route-planner rule
        // max(3·minTurn, width); a manual pass count fixes it to that many widths.
        double headlandMargin;
        int passes;
        if (headlandPasses > 0)
        {
            passes = headlandPasses;
            headlandMargin = passes * width;
        }
        else
        {
            headlandMargin = RoutePlanningService.RecommendHeadlandWidth(width, minTurn);
            passes = Math.Max(1, (int)Math.Round(headlandMargin / Math.Max(width, 0.1)));
        }

        // Turn ordering. Mirror the fork's per-pattern skip/block derivation, but
        // keyed off the explicit args rather than DisplayConfig.
        bool spiral = pattern == 3;
        bool cross = pattern == 2;
        int skipPasses = pattern switch
        {
            1 => Math.Max(0, skipCount),
            // Auto: skip the minimum so a normal U-turn fits the gap ((skip+1)·W ≥ 2R).
            0 => Math.Max(0, (int)Math.Ceiling(2.0 * turnRadius / Math.Max(width, 0.1)) - 1),
            2 => Math.Max(0, skipCount),
            _ => 0,
        };
        int blkSkip =
            pattern == 4 ? Math.Max(1, blockSkip)
            : (pattern == 2 && skipPasses == 0) ? Math.Max(1, blockSkip)
            : 0;
        double crossAngleRad = 90.0 * Math.PI / 180.0;
        double angleRad = angleDeg * Math.PI / 180.0;

        // Drive-to-start: current machine pose (skip if no fix).
        double vE = State.Vehicle.Easting, vN = State.Vehicle.Northing;
        Vec3? startPos = (vE == 0 && vN == 0)
            ? (Vec3?)null
            : new Vec3(vE, vN, State.Vehicle.Heading * Math.PI / 180.0);

        double clearance = _configStore.Guidance.UTurnDistanceFromBoundary;
        if (clearance < 0) clearance = 0;
        double cornerRadius = minTurn;
        double heading = LongestEdgeHeading(pts) + angleRad;

        RoutePlan? plan = spiral
            ? RoutePlanner.GenerateSpiral(pts, width, startPos, clearance, cornerRadius, cornerFill, inners)
            : cross
                ? RoutePlanner.GenerateCrossDrill(pts, width, turnRadius, headlandMargin, heading, crossAngleRad,
                    SwathPattern.Boustrophedon, passes, startPos, 0, false, false, clearance, skipPasses, blkSkip, cornerRadius)
                : RoutePlanner.GenerateBoustrophedon(pts, width, turnRadius, headlandMargin, heading,
                    SwathPattern.Boustrophedon, passes, startPos, 0, false, false, clearance, skipPasses, blkSkip, cornerRadius, inners);

        _currentRoutePlan = plan;
        if (plan == null)
        {
            StatusMessage = "Route planning produced no plan for this field";
            return;
        }

        var m = plan.Metadata;
        double areaHa = m.WorkDistanceMeters * m.ToolWidthMeters / 10000.0;
        StatusMessage = $"Route: {m.SwathCount} passes, {m.TurnCount} turns, " +
            $"{m.TotalDistanceMeters / 1000.0:F2} km, {areaHa:F1} ha, headland {headlandMargin:F1} m ({passes} laps)";
    }

    /// <summary>Discard the current route preview.</summary>
    public void ClearRoutePlan()
    {
        if (State.RecordedPath.IsDrivingRecordedPath) StopRouteDrive();
        _currentRoutePlan = null;
        StatusMessage = "Route cleared";
    }

    /// <summary>
    /// Drive the planned route in the simulator. Flatten the plan into recorded-path
    /// points and hand them to AgOpenWeb's recorded-path playback engine (Dubins
    /// approach to the start, then Pure-Pursuit along the path); then set a forward sim
    /// speed so it moves. Sections replay ON over worked passes (Swath/Headland) and OFF
    /// over turns/transit. The sim speed slider still adjusts pace; StopRouteDrive ends it.
    /// </summary>
    public void DriveRoute()
    {
        var plan = _currentRoutePlan;
        if (plan == null) { StatusMessage = "Plan a route first"; return; }

        const double driveSpeedKph = 8.0;

        // Flatten the plan to (E, N, working) — sections ON over worked passes.
        var raw = new List<(double e, double n, bool work)>();
        foreach (var seg in plan.Segments)
        {
            bool working = seg.Type == RouteSegmentType.Swath || seg.Type == RouteSegmentType.Headland;
            foreach (var p in seg.Points) raw.Add((p.Easting, p.Northing, working));
        }
        if (raw.Count < 2) { StatusMessage = "Route too short to drive"; return; }

        // Resample to a dense, uniform ~1 m spacing. AgOpenWeb's recorded-path follower
        // looks a fixed few POINTS ahead and snaps to the globally-nearest forward point,
        // so coarse route vertices (a straight swath is just 2 points tens of metres apart)
        // make the look-ahead overshoot and can let the vehicle jump onto an adjacent lap.
        // Dense points keep the nearest point sequential and the look-ahead ~2-3 m.
        const double step = 1.0;
        var pts = new List<RecPathPoint>(raw.Count * 8);
        pts.Add(new RecPathPoint(raw[0].e, raw[0].n, 0, driveSpeedKph, raw[0].work));
        double carry = 0; // distance already travelled past the last emitted point
        for (int i = 1; i < raw.Count; i++)
        {
            var a = raw[i - 1]; var b = raw[i];
            double dE = b.e - a.e, dN = b.n - a.n;
            double len = Math.Sqrt(dE * dE + dN * dN);
            if (len < 1e-9) continue;
            double d = step - carry;                       // first new point on this segment
            for (; d <= len; d += step)
            {
                double t = d / len;
                pts.Add(new RecPathPoint(a.e + dE * t, a.n + dN * t, 0, driveSpeedKph, b.work));
            }
            carry = len - (d - step);                      // leftover into the next segment
        }
        // Point headings: face the next point (the last keeps the previous heading).
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p = pts[i]; var q = pts[i + 1];
            p.Heading = Math.Atan2(q.Easting - p.Easting, q.Northing - p.Northing);
            pts[i] = p;
        }
        if (pts.Count < 5) { StatusMessage = "Route too short to drive"; return; }

        State.RecordedPath.RecordedPoints = pts;
        State.RecordedPath.CurrentPositionIndex = 0;
        if (!StartDrivingRecordedPath())
        {
            StatusMessage = "Couldn't start driving the route";
            return;
        }
        _haltSimAtRouteEnd = true;
        if (IsSimulatorEnabled) SimulatorSpeedKph = driveSpeedKph; // get moving; slider still adjusts
        StatusMessage = "Driving route…";
    }

    /// <summary>Stop driving the route and halt the simulator.</summary>
    public void StopRouteDrive()
    {
        _haltSimAtRouteEnd = false;
        StopDrivingRecordedPath();
        if (IsSimulatorEnabled) SimulatorSpeedKph = 0;
        StatusMessage = "Route drive stopped";
    }

    /// <summary>
    /// The current plan as JSON for the web preview, in the active map plane (metres):
    /// <c>{"segments":[{"type":"Swath","pts":[[e,n],…]},…],"meta":{…}}</c>.
    /// Coordinates are emitted at 0.1 m precision — plenty for a visual preview.
    /// Empty object when there's no plan.
    /// </summary>
    public string GetRoutePlanJson()
    {
        var plan = _currentRoutePlan;
        if (plan == null) return "{}";
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(64 * 1024);
        sb.Append("{\"segments\":[");
        bool firstSeg = true;
        foreach (var seg in plan.Segments)
        {
            if (seg.Points == null || seg.Points.Count == 0) continue;
            if (!firstSeg) sb.Append(',');
            firstSeg = false;
            sb.Append("{\"type\":\"").Append(seg.Type).Append("\",\"pts\":[");
            bool firstPt = true;
            foreach (var p in seg.Points)
            {
                if (!firstPt) sb.Append(',');
                firstPt = false;
                sb.Append('[')
                  .Append(p.Easting.ToString("0.0", inv)).Append(',')
                  .Append(p.Northing.ToString("0.0", inv)).Append(']');
            }
            sb.Append("]}");
        }
        sb.Append("],\"meta\":{");
        var m = plan.Metadata;
        sb.Append("\"swaths\":").Append(m.SwathCount.ToString(inv))
          .Append(",\"turns\":").Append(m.TurnCount.ToString(inv))
          .Append(",\"distanceM\":").Append(m.TotalDistanceMeters.ToString("0.0", inv))
          .Append(",\"toolWidthM\":").Append(m.ToolWidthMeters.ToString("0.00", inv))
          .Append("}}");
        return sb.ToString();
    }

    /// <summary>Heading (north-referenced) of the longest edge of a polygon.</summary>
    private static double LongestEdgeHeading(IReadOnlyList<Vec2> poly)
    {
        double bestLenSq = -1, bestHeading = 0;
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            double dE = b.Easting - a.Easting, dN = b.Northing - a.Northing;
            double lenSq = dE * dE + dN * dN;
            if (lenSq > bestLenSq)
            {
                bestLenSq = lenSq;
                bestHeading = Math.Atan2(dE, dN);
            }
        }
        return bestHeading;
    }
}
