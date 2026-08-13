// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Dubins path primitives — shortest curvature-limited path between two oriented
// poses at a fixed minimum turning radius. Ported from the AgOpenGPS core Dubins
// implementation (originally erik.nordeus@gmail.com's Unity code) and adapted to
// AgOpenWeb's Vec2/Vec3. Used by the route planner's headland turn generator so
// every turn is guaranteed drivable (curvature never exceeds 1/R).

using System;
using System.Collections.Generic;
using AgOpenWeb.Models.Base;

namespace AgOpenWeb.Services.RoutePlanning;

internal enum DubinsPathType { RSR, LSL, RSL, LSR, RLR, LRL }

/// <summary>
/// Generates all valid Dubins paths between two poses for a given turning radius.
/// The heading convention matches the rest of the app: dE = sin(heading),
/// dN = cos(heading).
/// </summary>
internal static class DubinsTurn
{
    // Sampling step along the path (meters). Fine enough for accurate boundary
    // containment tests and turn geometry without exploding point counts — the
    // generator runs up to a dozen times per turn during forward-extension search.
    private const double DriveDistance = 0.15;

    /// <summary>
    /// Every valid Dubins path between <paramref name="start"/> and
    /// <paramref name="goal"/> at radius <paramref name="r"/>, as densely-sampled
    /// Vec2 coordinate lists paired with total length, sorted shortest first.
    /// </summary>
    /// <param name="ccBlend">
    ///   Continuous-curvature blend distance (m). When &gt; 0, the region ±ccBlend
    ///   around each arc/line junction is replaced with a cubic Bezier so the
    ///   curvature ramps instead of stepping — the steering sweeps to lock rather
    ///   than being asked for an instant jump, so the tractor can actually track
    ///   the turn entry/exit. Ported from the upstream F2C branch (Bezier stand-in
    ///   for clothoid CC-Dubins; G1 blend, visually equivalent at these sizes).
    ///   Falls back to the plain path when segments are too short to cut.
    /// </param>
    public static List<(List<Vec2> Coords, double Length)> AllPaths(Vec3 start, Vec3 goal, double r, double ccBlend = 0)
    {
        var result = new List<(List<Vec2>, double)>();
        if (r <= 0.01) return result;

        var startPos = new Vec2(start.Easting, start.Northing);
        var goalPos = new Vec2(goal.Easting, goal.Northing);
        double startHeading = start.Heading, goalHeading = goal.Heading;

        var goalRight = GetRightCircle(goalPos, goalHeading, r);
        var goalLeft = GetLeftCircle(goalPos, goalHeading, r);
        var startRight = GetRightCircle(startPos, startHeading, r);
        var startLeft = GetLeftCircle(startPos, startHeading, r);

        var paths = new List<PathData>();

        // Skip RSR/LSL only when the two circles are IDENTICAL (degenerate tangent).
        // The upstream port required easting AND northing to differ, which silently
        // dropped RSR/LSL for the canonical aligned U-turn (both pass ends at the same
        // northing) — the sort then picked a 440-degree loop candidate instead of the
        // plain 180-degree turn.
        if ((startRight - goalRight).GetLengthSquared() > 1e-12)
            AddCSC(paths, startPos, goalPos, startRight, goalRight, false, false, r, DubinsPathType.RSR);
        if ((startLeft - goalLeft).GetLengthSquared() > 1e-12)
            AddCSC(paths, startPos, goalPos, startLeft, goalLeft, true, true, r, DubinsPathType.LSL);

        double twoRSq = (2.0 * r) * (2.0 * r);
        if ((startRight - goalLeft).GetLengthSquared() > twoRSq)
            AddCSCinner(paths, startPos, goalPos, startRight, goalLeft, false, r, DubinsPathType.RSL);
        if ((startLeft - goalRight).GetLengthSquared() > twoRSq)
            AddCSCinner(paths, startPos, goalPos, startLeft, goalRight, true, r, DubinsPathType.LSR);

        double fourRSq = (4.0 * r) * (4.0 * r);
        if ((startRight - goalRight).GetLengthSquared() < fourRSq)
            AddCCC(paths, startPos, goalPos, startRight, goalRight, false, r, DubinsPathType.RLR);
        if ((startLeft - goalLeft).GetLengthSquared() < fourRSq)
            AddCCC(paths, startPos, goalPos, startLeft, goalLeft, true, r, DubinsPathType.LRL);

        paths.Sort((a, b) => a.TotalLength.CompareTo(b.TotalLength));

        foreach (var pd in paths)
        {
            var coords = BuildCoords(pd, startPos, startHeading, goalPos, r);
            if (coords.Count < 2) continue;
            if (ccBlend > 0)
            {
                // Junction indices follow BuildCoords' layout: index 0 is the start,
                // then each AddArc call appends floor(len/step)+1 points. The error
                // smear preserves indices, so these stay valid on the final coords.
                int seg1 = (int)Math.Floor(pd.Length1 / DriveDistance);
                int seg2 = (int)Math.Floor(pd.Length2 / DriveDistance);
                coords = CcBlendJunctions(coords, seg1 + 1, seg1 + seg2 + 2, ccBlend);
            }
            result.Add((coords, pd.TotalLength));
        }
        return result;
    }

    /// <summary>
    /// Replace ±blend meters of waypoints around the two segment junctions with
    /// cubic Beziers whose control points lie along the local travel tangents
    /// (chord/3 — the canonical heuristic). Endpoints and the exact goal are
    /// never touched; if the cut regions would overlap each other or the path
    /// ends, the original (plain Dubins) coords are returned unchanged.
    /// </summary>
    private static List<Vec2> CcBlendJunctions(List<Vec2> raw, int junction12, int junction23, double blend)
    {
        int blendSteps = Math.Max(1, (int)Math.Round(blend / DriveDistance));
        int n = raw.Count;

        int cut1Start = Math.Max(1, junction12 - blendSteps);
        int cut1End = junction12 + blendSteps;
        int cut2Start = junction23 - blendSteps;
        int cut2End = Math.Min(n - 2, junction23 + blendSteps);

        if (cut1Start >= cut1End || cut2Start >= cut2End || cut1End >= cut2Start)
            return raw;
        if (cut1End >= n || cut2Start < 0)
            return raw;

        var result = new List<Vec2>(n);
        for (int i = 0; i <= cut1Start; i++) result.Add(raw[i]);
        AppendBezier(result, raw, cut1Start, cut1End);
        for (int i = cut1End + 1; i < cut2Start; i++) result.Add(raw[i]);
        AppendBezier(result, raw, cut2Start, cut2End);
        for (int i = cut2End + 1; i < n; i++) result.Add(raw[i]);
        return result;
    }

    private static void AppendBezier(List<Vec2> result, List<Vec2> raw, int a, int b)
    {
        var p0 = raw[a];
        var p3 = raw[b];
        // Travel tangents from finite differences: arriving direction at the cut
        // entry (G1 with the retained polyline before it), departing at the exit.
        var tin = Normalize(a > 0 ? p0 - raw[a - 1] : raw[a + 1] - p0);
        var tout = Normalize(b < raw.Count - 1 ? raw[b + 1] - p3 : p3 - raw[b - 1]);

        double chord = (p3 - p0).GetLength();
        double d = chord / 3.0;
        var p1 = new Vec2(p0.Easting + d * tin.Easting, p0.Northing + d * tin.Northing);
        var p2 = new Vec2(p3.Easting - d * tout.Easting, p3.Northing - d * tout.Northing);

        // Control-polygon length bounds the Bezier arc length — sets the sample count.
        double controlLen = (p1 - p0).GetLength() + (p2 - p1).GetLength() + (p3 - p2).GetLength();
        int steps = Math.Max(2, (int)Math.Ceiling(controlLen / DriveDistance));

        for (int k = 1; k <= steps; k++)
        {
            double t = (double)k / steps, mt = 1.0 - t;
            double w0 = mt * mt * mt, w1 = 3.0 * mt * mt * t, w2 = 3.0 * mt * t * t, w3 = t * t * t;
            result.Add(new Vec2(
                w0 * p0.Easting + w1 * p1.Easting + w2 * p2.Easting + w3 * p3.Easting,
                w0 * p0.Northing + w1 * p1.Northing + w2 * p2.Northing + w3 * p3.Northing));
        }
    }

    private static Vec2 Normalize(Vec2 v)
    {
        double len = v.GetLength();
        return len < 1e-12 ? new Vec2(0, 1) : new Vec2(v.Easting / len, v.Northing / len);
    }

    // ---- circle centers ----
    private static Vec2 GetRightCircle(Vec2 p, double heading, double r)
    {
        const double h = Math.PI / 2.0;
        return new Vec2(p.Easting + r * Math.Sin(heading + h), p.Northing + r * Math.Cos(heading + h));
    }

    private static Vec2 GetLeftCircle(Vec2 p, double heading, double r)
    {
        const double h = Math.PI / 2.0;
        return new Vec2(p.Easting + r * Math.Sin(heading - h), p.Northing + r * Math.Cos(heading - h));
    }

    // ---- CSC (RSR / LSL): outer tangent ----
    private static void AddCSC(List<PathData> paths, Vec2 startPos, Vec2 goalPos,
        Vec2 startCircle, Vec2 goalCircle, bool leftStart, bool leftGoal, double r, DubinsPathType type)
    {
        OuterTangent(startCircle, goalCircle, leftStart, r, out var st, out var gt);
        double l1 = ArcLength(startCircle, startPos, st, leftStart, r);
        double l2 = (st - gt).GetLength();
        double l3 = ArcLength(goalCircle, gt, goalPos, leftGoal, r);
        var pd = new PathData(l1, l2, l3, type) { Seg2Turning = false };
        pd.SetTurns(!leftStart, false, !leftGoal);
        paths.Add(pd);
    }

    // ---- CSC (RSL / LSR): inner tangent ----
    private static void AddCSCinner(List<PathData> paths, Vec2 startPos, Vec2 goalPos,
        Vec2 startCircle, Vec2 goalCircle, bool isLSR, double r, DubinsPathType type)
    {
        InnerTangent(startCircle, goalCircle, isLSR, r, out var st, out var gt);
        // RSL: start turns right, goal turns left. LSR: start left, goal right.
        bool startRight = !isLSR, goalRight = isLSR;
        double l1 = ArcLength(startCircle, startPos, st, !startRight, r);
        double l2 = (st - gt).GetLength();
        double l3 = ArcLength(goalCircle, gt, goalPos, !goalRight, r);
        var pd = new PathData(l1, l2, l3, type) { Seg2Turning = false };
        pd.SetTurns(startRight, false, goalRight);
        paths.Add(pd);
    }

    // ---- CCC (RLR / LRL) ----
    private static void AddCCC(List<PathData> paths, Vec2 startPos, Vec2 goalPos,
        Vec2 startCircle, Vec2 goalCircle, bool isLRL, double r, DubinsPathType type)
    {
        double d = (startCircle - goalCircle).GetLength();
        double theta = Math.Acos(d / (4.0 * r));
        var v1 = goalCircle - startCircle;
        theta = isLRL ? Math.Atan2(v1.Northing, v1.Easting) + theta
                      : Math.Atan2(v1.Northing, v1.Easting) - theta;
        var mid = new Vec2(startCircle.Easting + 2 * r * Math.Cos(theta),
                           startCircle.Northing + 2 * r * Math.Sin(theta));
        var st = mid + (startCircle - mid).Normalize() * r;
        var gt = mid + (goalCircle - mid).Normalize() * r;

        // RLR: start R, middle L, goal R. LRL: start L, middle R, goal L.
        bool startRight = !isLRL, midRight = isLRL, goalRight = !isLRL;
        double l1 = ArcLength(startCircle, startPos, st, !startRight, r);
        double l2 = ArcLength(mid, st, gt, !midRight, r);
        double l3 = ArcLength(goalCircle, gt, goalPos, !goalRight, r);
        var pd = new PathData(l1, l2, l3, type) { Seg2Turning = true };
        pd.SetTurns(startRight, midRight, goalRight);
        paths.Add(pd);
    }

    private static void OuterTangent(Vec2 startCircle, Vec2 goalCircle, bool isBottom, double r,
        out Vec2 startTangent, out Vec2 goalTangent)
    {
        const double h = Math.PI / 2.0;
        double theta = h + Math.Atan2(goalCircle.Northing - startCircle.Northing,
                                      goalCircle.Easting - startCircle.Easting);
        if (isBottom) theta += Math.PI;
        double x1 = startCircle.Easting + r * Math.Cos(theta);
        double z1 = startCircle.Northing + r * Math.Sin(theta);
        var dir = goalCircle - startCircle;
        startTangent = new Vec2(x1, z1);
        goalTangent = new Vec2(x1 + dir.Easting, z1 + dir.Northing);
    }

    private static void InnerTangent(Vec2 startCircle, Vec2 goalCircle, bool isBottom, double r,
        out Vec2 startTangent, out Vec2 goalTangent)
    {
        double d = (startCircle - goalCircle).GetLength();
        double theta = Math.Acos((2 * r) / d);
        if (isBottom) theta *= -1.0;
        theta += Math.Atan2(goalCircle.Northing - startCircle.Northing,
                            goalCircle.Easting - startCircle.Easting);
        double x1 = startCircle.Easting + r * Math.Cos(theta);
        double z1 = startCircle.Northing + r * Math.Sin(theta);
        double x1t = startCircle.Easting + 2.0 * r * Math.Cos(theta);
        double z1t = startCircle.Northing + 2.0 * r * Math.Sin(theta);
        var dir = goalCircle - new Vec2(x1t, z1t);
        startTangent = new Vec2(x1, z1);
        goalTangent = new Vec2(x1 + dir.Easting, z1 + dir.Northing);
    }

    private static double ArcLength(Vec2 center, Vec2 from, Vec2 to, bool isLeftCircle, double r)
    {
        var v1 = from - center;
        var v2 = to - center;
        double theta = Math.Atan2(v2.Northing, v2.Easting) - Math.Atan2(v1.Northing, v1.Easting);
        if (theta < 0.0 && isLeftCircle) theta += 2.0 * Math.PI;
        else if (theta > 0 && !isLeftCircle) theta -= 2.0 * Math.PI;
        return Math.Abs(theta * r);
    }

    private static List<Vec2> BuildCoords(PathData pd, Vec2 startPos, double startHeading, Vec2 goalPos, double r)
    {
        var path = new List<Vec2>();
        var pos = startPos;
        double theta = startHeading;
        path.Add(pos);

        AddArc(ref pos, ref theta, path, (int)Math.Floor(pd.Length1 / DriveDistance), true, pd.Seg1Right, r);
        AddArc(ref pos, ref theta, path, (int)Math.Floor(pd.Length2 / DriveDistance), pd.Seg2Turning, pd.Seg2Right, r);
        AddArc(ref pos, ref theta, path, (int)Math.Floor(pd.Length3 / DriveDistance), true, pd.Seg3Right, r);

        // The Euler integration drifts slightly off the analytic goal; snapping the last
        // point there would leave a short stub in a wrong direction (reads as a kink at
        // the junction). Smear the residual error linearly along the whole path instead —
        // start stays exact, end lands exactly on the goal, curvature barely changes.
        var err = goalPos - path[^1];
        int n = path.Count;
        if (n < 2) { path.Add(goalPos); return path; }
        for (int i = 1; i < n; i++)
            path[i] = new Vec2(path[i].Easting + err.Easting * i / (n - 1),
                               path[i].Northing + err.Northing * i / (n - 1));
        return path;
    }

    private static void AddArc(ref Vec2 pos, ref double theta, List<Vec2> path,
        int segments, bool isTurning, bool isRight, double r)
    {
        for (int i = 0; i <= segments; i++)
        {
            pos.Easting += DriveDistance * Math.Sin(theta);
            pos.Northing += DriveDistance * Math.Cos(theta);
            if (isTurning) theta += (DriveDistance / r) * (isRight ? 1.0 : -1.0);
            path.Add(pos);
        }
    }

    private sealed class PathData
    {
        public readonly double TotalLength, Length1, Length2, Length3;
        public readonly DubinsPathType Type;
        public bool Seg2Turning;
        public bool Seg1Right, Seg2Right, Seg3Right;

        public PathData(double l1, double l2, double l3, DubinsPathType type)
        {
            Length1 = l1; Length2 = l2; Length3 = l3;
            TotalLength = l1 + l2 + l3; Type = type;
        }

        public void SetTurns(bool s1, bool s2, bool s3) { Seg1Right = s1; Seg2Right = s2; Seg3Right = s3; }
    }
}
