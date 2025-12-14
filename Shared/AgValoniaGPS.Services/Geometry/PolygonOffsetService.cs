using System.Collections.Generic;
using System.Linq;
using AgValoniaGPS.Models.Base;
using Clipper2Lib;

namespace AgValoniaGPS.Services.Geometry;

/// <summary>
/// Service for creating offset polygons using Clipper2 library.
/// Used for headland generation from field boundaries.
/// </summary>
public class PolygonOffsetService : IPolygonOffsetService
{
    // Clipper2 uses integer coordinates, so we scale by this factor for precision
    private const double Scale = 1000.0;

    /// <summary>
    /// Create an inward offset polygon from a boundary.
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points (should be clockwise for outer boundary)</param>
    /// <param name="offsetDistance">Inward offset distance in meters (positive = inward/shrink)</param>
    /// <param name="joinType">How to handle corners (Round, Miter, Square)</param>
    /// <returns>Offset polygon points, or null if offset collapses the polygon</returns>
    public List<Vec2>? CreateInwardOffset(List<Vec2> boundaryPoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (boundaryPoints == null || boundaryPoints.Count < 3)
            return null;

        if (offsetDistance <= 0)
            return new List<Vec2>(boundaryPoints);

        // Use simple perpendicular offset approach (like original AgOpenGPS)
        // This preserves smooth curves by moving each point perpendicular to its heading
        System.Diagnostics.Debug.WriteLine($"[PolygonOffset] Using perpendicular offset: {boundaryPoints.Count} points, {offsetDistance}m inward");

        // First, calculate headings for each point
        var pointsWithHeadings = CalculatePointHeadings(boundaryPoints);

        // Create offset points by moving perpendicular to heading
        var offsetPoints = new List<Vec2>(pointsWithHeadings.Count);
        double distSqAway = (offsetDistance * offsetDistance) - 0.01;

        // Optimization: only check nearby boundary points (within a window)
        // This reduces O(n²) to O(n*k) where k is the window size
        int windowSize = System.Math.Min(20, boundaryPoints.Count / 4);

        for (int i = 0; i < pointsWithHeadings.Count; i++)
        {
            var pt = pointsWithHeadings[i];
            // Move perpendicular to heading (inward = right side for clockwise boundary)
            double offsetX = pt.Easting - (System.Math.Sin(pt.Heading + System.Math.PI / 2) * offsetDistance);
            double offsetY = pt.Northing - (System.Math.Cos(pt.Heading + System.Math.PI / 2) * offsetDistance);

            var newPoint = new Vec2(offsetX, offsetY);

            // Filter out points that would be closer than offset distance to original boundary
            // Only check nearby boundary points to avoid O(n²) performance
            bool tooClose = false;
            int checkStart = System.Math.Max(0, i - windowSize);
            int checkEnd = System.Math.Min(boundaryPoints.Count, i + windowSize);
            for (int j = checkStart; j < checkEnd; j++)
            {
                double dx = newPoint.Easting - boundaryPoints[j].Easting;
                double dy = newPoint.Northing - boundaryPoints[j].Northing;
                if (dx * dx + dy * dy < distSqAway)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                // Also filter points too close to previous offset point
                if (offsetPoints.Count > 0)
                {
                    var lastPt = offsetPoints[offsetPoints.Count - 1];
                    double dx = newPoint.Easting - lastPt.Easting;
                    double dy = newPoint.Northing - lastPt.Northing;
                    if (dx * dx + dy * dy > 0.25) // Min 0.5m spacing
                    {
                        offsetPoints.Add(newPoint);
                    }
                }
                else
                {
                    offsetPoints.Add(newPoint);
                }
            }
        }

        if (offsetPoints.Count < 3)
        {
            System.Diagnostics.Debug.WriteLine($"[PolygonOffset] Offset collapsed - only {offsetPoints.Count} points");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"[PolygonOffset] Final output: {offsetPoints.Count} points");
        return offsetPoints;
    }

    /// <summary>
    /// Create an outward offset polygon from a boundary.
    /// Used for inner boundaries (islands) where headland goes outward.
    /// </summary>
    /// <param name="boundaryPoints">Inner boundary points</param>
    /// <param name="offsetDistance">Outward offset distance in meters</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset polygon points</returns>
    public List<Vec2>? CreateOutwardOffset(List<Vec2> boundaryPoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (boundaryPoints == null || boundaryPoints.Count < 3)
            return null;

        if (offsetDistance <= 0)
            return new List<Vec2>(boundaryPoints);

        var path = new Path64(boundaryPoints.Count);
        foreach (var pt in boundaryPoints)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        var clipperOffset = new ClipperOffset();

        // Set arc tolerance for smooth curves (smaller = smoother)
        clipperOffset.ArcTolerance = 25;

        JoinType clipperJoinType = joinType switch
        {
            OffsetJoinType.Miter => JoinType.Miter,
            OffsetJoinType.Square => JoinType.Square,
            _ => JoinType.Round
        };

        clipperOffset.AddPath(path, clipperJoinType, EndType.Polygon);

        var solution = new Paths64();
        // Positive offset = expand/outward
        clipperOffset.Execute(offsetDistance * Scale, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
            return null;

        var result = new List<Vec2>(solution[0].Count);
        foreach (var pt in solution[0])
        {
            result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
        }

        return result;
    }

    /// <summary>
    /// Create multiple concentric offset polygons (for multi-pass headlands).
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points</param>
    /// <param name="offsetDistance">Distance per pass in meters</param>
    /// <param name="passes">Number of passes</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>List of offset polygons from outermost to innermost</returns>
    public List<List<Vec2>> CreateMultiPassOffset(List<Vec2> boundaryPoints, double offsetDistance, int passes, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        var result = new List<List<Vec2>>();

        if (boundaryPoints == null || boundaryPoints.Count < 3 || passes <= 0)
            return result;

        var currentBoundary = boundaryPoints;

        for (int i = 0; i < passes; i++)
        {
            var offset = CreateInwardOffset(currentBoundary, offsetDistance, joinType);
            if (offset == null || offset.Count < 3)
                break;

            result.Add(offset);
            currentBoundary = offset;
        }

        return result;
    }

    /// <summary>
    /// Create an offset of an open polyline (not closed polygon).
    /// Used for headland segments from boundary clips.
    /// </summary>
    /// <param name="linePoints">Open polyline points</param>
    /// <param name="offsetDistance">Offset distance in meters (positive = left of travel direction)</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset line points, or null if offset fails</returns>
    public List<Vec2>? CreateLineOffset(List<Vec2> linePoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (linePoints == null || linePoints.Count < 2)
            return null;

        if (offsetDistance == 0)
            return new List<Vec2>(linePoints);

        // Convert to Clipper2 path (scaled to integers)
        var path = new Path64(linePoints.Count);
        foreach (var pt in linePoints)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        // Create offset for open path
        var clipperOffset = new ClipperOffset();

        // Set arc tolerance for smooth curves (smaller = smoother)
        clipperOffset.ArcTolerance = 25;

        JoinType clipperJoinType = joinType switch
        {
            OffsetJoinType.Miter => JoinType.Miter,
            OffsetJoinType.Square => JoinType.Square,
            _ => JoinType.Round
        };

        // EndType.Round for open path with rounded ends
        clipperOffset.AddPath(path, clipperJoinType, EndType.Round);

        var solution = new Paths64();
        clipperOffset.Execute(offsetDistance * Scale, solution);

        if (solution.Count == 0 || solution[0].Count < 2)
            return null;

        // The result is a closed polygon around the line
        // We need to extract just one side - find the points closest to our original offset direction
        var result = new List<Vec2>(solution[0].Count);
        foreach (var pt in solution[0])
        {
            result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
        }

        // For a simple line offset, extract the relevant half of the buffer polygon
        return ExtractOffsetSide(linePoints, result, offsetDistance);
    }

    /// <summary>
    /// Apply Chaikin's corner cutting algorithm to smooth a closed polygon.
    /// This algorithm cuts corners without overshoot, staying within the convex hull.
    /// </summary>
    /// <param name="polygon">Input polygon points</param>
    /// <param name="iterations">Number of smoothing iterations (more = smoother but smaller)</param>
    /// <returns>Smoothed polygon</returns>
    private List<Vec2> ChaikinSmooth(List<Vec2> polygon, int iterations)
    {
        if (polygon.Count < 3 || iterations < 1)
            return polygon;

        var current = polygon;

        for (int iter = 0; iter < iterations; iter++)
        {
            var next = new List<Vec2>(current.Count * 2);

            for (int i = 0; i < current.Count; i++)
            {
                var p0 = current[i];
                var p1 = current[(i + 1) % current.Count];

                // Chaikin uses 1/4 and 3/4 points along each edge
                double q0x = 0.75 * p0.Easting + 0.25 * p1.Easting;
                double q0y = 0.75 * p0.Northing + 0.25 * p1.Northing;
                double q1x = 0.25 * p0.Easting + 0.75 * p1.Easting;
                double q1y = 0.25 * p0.Northing + 0.75 * p1.Northing;

                next.Add(new Vec2(q0x, q0y));
                next.Add(new Vec2(q1x, q1y));
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Densify a polygon by adding interpolated points where segments are too long.
    /// This ensures curves are well-approximated with many short segments.
    /// </summary>
    /// <param name="polygon">Input polygon points</param>
    /// <param name="maxSpacing">Maximum allowed distance between consecutive points</param>
    /// <returns>Densified polygon with additional interpolated points</returns>
    private List<Vec2> DensifyPolygon(List<Vec2> polygon, double maxSpacing)
    {
        if (polygon.Count < 3 || maxSpacing <= 0)
            return polygon;

        var result = new List<Vec2>();

        for (int i = 0; i < polygon.Count; i++)
        {
            var p0 = polygon[i];
            var p1 = polygon[(i + 1) % polygon.Count];

            // Always add the current point
            result.Add(p0);

            // Calculate segment length
            double dx = p1.Easting - p0.Easting;
            double dy = p1.Northing - p0.Northing;
            double segmentLength = System.Math.Sqrt(dx * dx + dy * dy);

            // Add interpolated points if segment is too long
            if (segmentLength > maxSpacing)
            {
                int subdivisions = (int)System.Math.Ceiling(segmentLength / maxSpacing);
                for (int j = 1; j < subdivisions; j++)
                {
                    double t = (double)j / subdivisions;
                    double x = p0.Easting + t * dx;
                    double y = p0.Northing + t * dy;
                    result.Add(new Vec2(x, y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Calculate the perimeter of a closed polygon
    /// </summary>
    private double CalculatePolygonPerimeter(List<Vec2> polygon)
    {
        double perimeter = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            perimeter += System.Math.Sqrt(
                (p2.Easting - p1.Easting) * (p2.Easting - p1.Easting) +
                (p2.Northing - p1.Northing) * (p2.Northing - p1.Northing));
        }
        return perimeter;
    }

    /// <summary>
    /// Resample a closed polygon to have evenly spaced points.
    /// This smooths out angular artifacts from offset operations.
    /// </summary>
    private List<Vec2> ResamplePolygon(List<Vec2> polygon, double targetSpacing)
    {
        if (polygon.Count < 3 || targetSpacing <= 0)
            return polygon;

        // Build cumulative distance array for the closed polygon
        var cumDist = new double[polygon.Count + 1];
        cumDist[0] = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            double dx = p2.Easting - p1.Easting;
            double dy = p2.Northing - p1.Northing;
            cumDist[i + 1] = cumDist[i] + System.Math.Sqrt(dx * dx + dy * dy);
        }

        double totalLength = cumDist[polygon.Count];
        int numPoints = System.Math.Max(3, (int)System.Math.Round(totalLength / targetSpacing));
        double actualSpacing = totalLength / numPoints;

        var result = new List<Vec2>(numPoints);

        for (int i = 0; i < numPoints; i++)
        {
            double targetDist = i * actualSpacing;

            // Find which segment this distance falls into
            int segIdx = 0;
            for (int j = 1; j <= polygon.Count; j++)
            {
                if (cumDist[j] >= targetDist)
                {
                    segIdx = j - 1;
                    break;
                }
            }

            // Interpolate within the segment
            var p1 = polygon[segIdx];
            var p2 = polygon[(segIdx + 1) % polygon.Count];
            double segStart = cumDist[segIdx];
            double segEnd = cumDist[segIdx + 1];
            double segLength = segEnd - segStart;

            double t = segLength > 1e-10 ? (targetDist - segStart) / segLength : 0;
            t = System.Math.Clamp(t, 0, 1);

            double x = p1.Easting + t * (p2.Easting - p1.Easting);
            double y = p1.Northing + t * (p2.Northing - p1.Northing);
            result.Add(new Vec2(x, y));
        }

        return result;
    }

    /// <summary>
    /// Extract one side from a buffer polygon around a line
    /// </summary>
    private List<Vec2>? ExtractOffsetSide(List<Vec2> originalLine, List<Vec2> bufferPolygon, double offsetDistance)
    {
        if (bufferPolygon.Count < 4)
            return bufferPolygon;

        // Find the points in the buffer that are on the offset side
        // by checking which points are approximately offsetDistance away from the original line
        var result = new List<Vec2>();
        double targetDist = System.Math.Abs(offsetDistance);
        double tolerance = targetDist * 0.5; // Allow some variation

        foreach (var bufferPt in bufferPolygon)
        {
            // Find minimum distance to original line
            double minDist = double.MaxValue;
            for (int i = 0; i < originalLine.Count - 1; i++)
            {
                double dist = PointToSegmentDistance(bufferPt, originalLine[i], originalLine[i + 1]);
                minDist = System.Math.Min(minDist, dist);
            }

            // Keep points that are roughly the right distance away
            if (System.Math.Abs(minDist - targetDist) < tolerance)
            {
                result.Add(bufferPt);
            }
        }

        // If we got enough points, sort them along the original line direction
        if (result.Count >= 2)
        {
            // Sort by projection onto original line direction
            var lineDir = new Vec2(
                originalLine[originalLine.Count - 1].Easting - originalLine[0].Easting,
                originalLine[originalLine.Count - 1].Northing - originalLine[0].Northing);
            double len = System.Math.Sqrt(lineDir.Easting * lineDir.Easting + lineDir.Northing * lineDir.Northing);
            if (len > 0)
            {
                lineDir = new Vec2(lineDir.Easting / len, lineDir.Northing / len);
                result.Sort((a, b) =>
                {
                    double projA = a.Easting * lineDir.Easting + a.Northing * lineDir.Northing;
                    double projB = b.Easting * lineDir.Easting + b.Northing * lineDir.Northing;
                    return projA.CompareTo(projB);
                });
            }
            return result;
        }

        // Fallback: just return first half of buffer (rough approximation)
        return bufferPolygon.Take(bufferPolygon.Count / 2).ToList();
    }

    /// <summary>
    /// Calculate distance from a point to a line segment
    /// </summary>
    private double PointToSegmentDistance(Vec2 point, Vec2 segA, Vec2 segB)
    {
        double dx = segB.Easting - segA.Easting;
        double dy = segB.Northing - segA.Northing;
        double lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-10)
            return System.Math.Sqrt(
                (point.Easting - segA.Easting) * (point.Easting - segA.Easting) +
                (point.Northing - segA.Northing) * (point.Northing - segA.Northing));

        double t = System.Math.Max(0, System.Math.Min(1,
            ((point.Easting - segA.Easting) * dx + (point.Northing - segA.Northing) * dy) / lenSq));

        double projX = segA.Easting + t * dx;
        double projY = segA.Northing + t * dy;

        return System.Math.Sqrt(
            (point.Easting - projX) * (point.Easting - projX) +
            (point.Northing - projY) * (point.Northing - projY));
    }

    /// <summary>
    /// Calculate headings for each point in the polygon based on adjacent points.
    /// </summary>
    /// <param name="points">Polygon points</param>
    /// <returns>Points with headings as Vec3 (X, Y, Heading in radians)</returns>
    public List<Vec3> CalculatePointHeadings(List<Vec2> points)
    {
        var result = new List<Vec3>(points.Count);

        if (points == null || points.Count < 3)
            return result;

        for (int i = 0; i < points.Count; i++)
        {
            var prev = points[(i - 1 + points.Count) % points.Count];
            var next = points[(i + 1) % points.Count];

            // Calculate heading from direction vector between prev and next
            double dx = next.Easting - prev.Easting;
            double dy = next.Northing - prev.Northing;
            double heading = System.Math.Atan2(dx, dy);

            result.Add(new Vec3(points[i].Easting, points[i].Northing, heading));
        }

        return result;
    }
}

/// <summary>
/// Corner join types for polygon offset
/// </summary>
public enum OffsetJoinType
{
    /// <summary>Round corners (smooth)</summary>
    Round,
    /// <summary>Miter corners (sharp, extended)</summary>
    Miter,
    /// <summary>Square corners (flat)</summary>
    Square
}
