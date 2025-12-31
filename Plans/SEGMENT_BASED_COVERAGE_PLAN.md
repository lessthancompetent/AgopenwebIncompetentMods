# Segment-Based Coverage Detection Plan

## Overview

Replace point-based coverage detection with segment-based detection using coordinate transformation. This addresses a fundamental architectural flaw identified by Brian (AgOpenGPS creator) where checking single points misses coverage gaps and edge cases.

## Status: NOT STARTED

---

## The Problem

### Current Point-Based Approach

```
Section segment:   [L]━━━━━━━━━━━━━━━━━━━[R]
                        ↑ (center point check)

Coverage patches:  ████████        ████████
                          ↑ gap ↑

Current result: "Not covered" (center hits gap) or "Covered" (center hits patch)
Actual situation: 80% covered, 20% gap - section should detect partial overlap
```

**Issues:**
- Single point can miss gaps within section width
- Section endpoints may straddle coverage patches
- Boundary/headland checks only at center point
- No concept of "percentage covered"

### Segment-Based Solution

Check the entire section width (left edge to right edge) against coverage triangles, returning overlap percentage rather than boolean.

---

## Proposed Approach: Coordinate Transform Method

Credit: Approach suggested by Qt developer working on similar problem.

### Key Insight

Transform coverage triangles into a local coordinate system where:
- Section segment becomes the X-axis: `(-halfWidth, 0)` to `(+halfWidth, 0)`
- "Does triangle overlap section?" becomes "Does triangle cross Y=0?"
- Finding overlap is just linear interpolation to find X intercepts

```
World Space:                    Local Space (transformed):

    ◢████                           Y
   ◢█████  ←triangle                ↑
  ◢██████                           |    ◢██
  [section]→                   ─────┼────◢██─────→ X (section)
     ↑                             ━━━━━━━━━━
  heading                      -halfWidth  +halfWidth
```

### Why This Is Fast

| Task | Direct Intersection | Transform Method |
|------|---------------------|------------------|
| Setup | None | Transform 3 vertices (~12 ops) |
| "Does it overlap?" | 3 edge-segment tests (~50 ops) | 3 sign checks (3 ops) |
| "Where?" | Solve line equations | Simple interpolation (~4 ops each) |
| Early exit | Complex | Trivial (all Y same sign = skip) |

---

## Implementation Design

### Phase 1: New Data Structures

#### CoverageResult Record
```csharp
// New file: Shared/AgValoniaGPS.Models/Coverage/CoverageResult.cs

/// <summary>
/// Result of segment-based coverage analysis.
/// </summary>
public readonly record struct CoverageResult(
    /// <summary>Coverage from 0.0 (none) to 1.0 (full)</summary>
    double CoveragePercent,

    /// <summary>True if any overlap exists</summary>
    bool HasAnyOverlap,

    /// <summary>True if 100% covered (within tolerance)</summary>
    bool IsFullyCovered,

    /// <summary>Total uncovered length in meters</summary>
    double UncoveredLength
);
```

#### BoundaryResult Record
```csharp
// New file: Shared/AgValoniaGPS.Models/Boundary/BoundaryResult.cs

/// <summary>
/// Result of segment-based boundary analysis.
/// </summary>
public readonly record struct BoundaryResult(
    /// <summary>Entire segment is inside boundary</summary>
    bool IsFullyInside,

    /// <summary>Entire segment is outside boundary</summary>
    bool IsFullyOutside,

    /// <summary>Segment crosses boundary edge</summary>
    bool CrossesBoundary,

    /// <summary>Percentage of segment inside boundary</summary>
    double InsidePercent
);
```

### Phase 2: Geometry Utilities

#### LocalCoordinate Transform
```csharp
// Add to: Shared/AgValoniaGPS.Models/Base/GeometryMath.cs

/// <summary>
/// Transform a world point to local coordinates where the section
/// center is at origin and section heading aligns with X-axis.
/// </summary>
public static Vec2 ToLocalCoords(
    Vec2 worldPoint,
    Vec2 sectionCenter,
    double sectionHeading)
{
    double cos = Math.Cos(-sectionHeading);
    double sin = Math.Sin(-sectionHeading);

    double dx = worldPoint.Easting - sectionCenter.Easting;
    double dy = worldPoint.Northing - sectionCenter.Northing;

    return new Vec2(
        dx * cos - dy * sin,  // X in local coords
        dx * sin + dy * cos   // Y in local coords
    );
}

/// <summary>
/// Optimized transform using precomputed sin/cos.
/// </summary>
public static Vec2 ToLocalCoords(
    Vec2 worldPoint,
    Vec2 sectionCenter,
    double cos,
    double sin)
{
    double dx = worldPoint.Easting - sectionCenter.Easting;
    double dy = worldPoint.Northing - sectionCenter.Northing;

    return new Vec2(dx * cos - dy * sin, dx * sin + dy * cos);
}
```

#### Y-Axis Intercept Calculation
```csharp
/// <summary>
/// Find X coordinate where edge from p1 to p2 crosses Y=0.
/// Returns null if edge doesn't cross Y=0.
/// </summary>
public static double? GetXInterceptAtYZero(Vec2 p1, Vec2 p2)
{
    // Both above or both below Y=0?
    if ((p1.Northing > 0) == (p2.Northing > 0))
        return null;

    // Linear interpolation: find t where Y = 0
    double t = -p1.Northing / (p2.Northing - p1.Northing);
    return p1.Easting + t * (p2.Easting - p1.Easting);
}
```

### Phase 3: Coverage Service Updates

#### Interface Addition
```csharp
// Update: Shared/AgValoniaGPS.Services/Interfaces/ICoverageMapService.cs

public interface ICoverageMapService
{
    // Existing methods...
    bool IsPointCovered(double easting, double northing);

    // NEW: Segment-based coverage check
    /// <summary>
    /// Calculate coverage for a section segment using transform method.
    /// </summary>
    /// <param name="sectionCenter">Center point of section in world coords</param>
    /// <param name="heading">Section heading in radians</param>
    /// <param name="halfWidth">Half the section width in meters</param>
    CoverageResult GetSegmentCoverage(Vec2 sectionCenter, double heading, double halfWidth);

    // NEW: Multi-section batch check (optimization)
    /// <summary>
    /// Check coverage for multiple sections in one pass.
    /// More efficient than individual calls due to shared frustum culling.
    /// </summary>
    IReadOnlyList<CoverageResult> GetSegmentCoverageBatch(
        IReadOnlyList<(Vec2 Center, double Heading, double HalfWidth)> sections);
}
```

#### Implementation
```csharp
// Update: Shared/AgValoniaGPS.Services/Coverage/CoverageMapService.cs

public CoverageResult GetSegmentCoverage(Vec2 sectionCenter, double heading, double halfWidth)
{
    // Precompute transform
    double cos = Math.Cos(-heading);
    double sin = Math.Sin(-heading);

    // Collect coverage intervals along X-axis
    var intervals = new List<(double xStart, double xEnd)>();

    // Frustum culling radius (section width + margin)
    double cullRadius = halfWidth + 5.0; // 5m margin

    foreach (var patch in _patches)
    {
        if (!patch.IsRenderable) continue;

        // Quick bounding box rejection
        if (!IsPatchNearPoint(patch, sectionCenter, cullRadius))
            continue;

        // Check each triangle in the strip
        for (int i = 1; i < patch.Vertices.Count - 2; i++)
        {
            var interval = GetTriangleXInterval(
                patch.Vertices[i],
                patch.Vertices[i + 1],
                patch.Vertices[i + 2],
                sectionCenter, cos, sin, halfWidth);

            if (interval.HasValue)
                intervals.Add(interval.Value);
        }
    }

    // Merge overlapping intervals
    var merged = MergeIntervals(intervals);

    // Calculate total coverage
    double totalWidth = halfWidth * 2;
    double coveredWidth = merged.Sum(i => i.xEnd - i.xStart);
    double coveragePercent = coveredWidth / totalWidth;

    return new CoverageResult(
        CoveragePercent: coveragePercent,
        HasAnyOverlap: coveredWidth > 0.001,
        IsFullyCovered: coveragePercent > 0.99,
        UncoveredLength: totalWidth - coveredWidth
    );
}

private (double xStart, double xEnd)? GetTriangleXInterval(
    Vec3 v0, Vec3 v1, Vec3 v2,
    Vec2 center, double cos, double sin, double halfWidth)
{
    // Transform to local coords
    var a = GeometryMath.ToLocalCoords(new Vec2(v0.Easting, v0.Northing), center, cos, sin);
    var b = GeometryMath.ToLocalCoords(new Vec2(v1.Easting, v1.Northing), center, cos, sin);
    var c = GeometryMath.ToLocalCoords(new Vec2(v2.Easting, v2.Northing), center, cos, sin);

    // Quick reject: all above or all below X-axis?
    if ((a.Northing > 0 && b.Northing > 0 && c.Northing > 0) ||
        (a.Northing < 0 && b.Northing < 0 && c.Northing < 0))
        return null;

    // Find X intercepts where edges cross Y=0
    var xIntercepts = new List<double>(4);

    var x1 = GeometryMath.GetXInterceptAtYZero(a, b);
    var x2 = GeometryMath.GetXInterceptAtYZero(b, c);
    var x3 = GeometryMath.GetXInterceptAtYZero(c, a);

    if (x1.HasValue) xIntercepts.Add(x1.Value);
    if (x2.HasValue) xIntercepts.Add(x2.Value);
    if (x3.HasValue) xIntercepts.Add(x3.Value);

    // Handle vertices exactly on the axis
    const double epsilon = 0.001;
    if (Math.Abs(a.Northing) < epsilon) xIntercepts.Add(a.Easting);
    if (Math.Abs(b.Northing) < epsilon) xIntercepts.Add(b.Easting);
    if (Math.Abs(c.Northing) < epsilon) xIntercepts.Add(c.Easting);

    if (xIntercepts.Count < 2)
        return null;

    double xMin = xIntercepts.Min();
    double xMax = xIntercepts.Max();

    // Clip to section bounds
    xMin = Math.Max(xMin, -halfWidth);
    xMax = Math.Min(xMax, halfWidth);

    if (xMax <= xMin)
        return null;

    return (xMin, xMax);
}

private List<(double xStart, double xEnd)> MergeIntervals(List<(double xStart, double xEnd)> intervals)
{
    if (intervals.Count == 0)
        return new List<(double, double)>();

    // Sort by start position
    intervals.Sort((a, b) => a.xStart.CompareTo(b.xStart));

    var merged = new List<(double xStart, double xEnd)>();
    var current = intervals[0];

    for (int i = 1; i < intervals.Count; i++)
    {
        if (intervals[i].xStart <= current.xEnd)
        {
            // Overlapping - extend current
            current.xEnd = Math.Max(current.xEnd, intervals[i].xEnd);
        }
        else
        {
            // Gap - save current, start new
            merged.Add(current);
            current = intervals[i];
        }
    }

    merged.Add(current);
    return merged;
}
```

### Phase 4: Section Control Integration

#### Update SectionControlService
```csharp
// Update: Shared/AgValoniaGPS.Services/Section/SectionControlService.cs

// Add configurable overlap threshold
private const double COVERAGE_OVERLAP_THRESHOLD = 0.70; // 70% covered = "covered"

// In Update() method, replace:
// OLD:
var lookOnCovered = _coverageMapService.IsPointCovered(onCheckPoint.Easting, onCheckPoint.Northing);

// NEW:
var (leftEdge, rightEdge) = GetSectionWorldPosition(index, toolPosition, toolHeading);
var sectionCenter = new Vec2(
    (leftEdge.Easting + rightEdge.Easting) / 2,
    (leftEdge.Northing + rightEdge.Northing) / 2);
var halfWidth = Distance(leftEdge, rightEdge) / 2;

// Project center forward for look-ahead
var lookAheadCenter = ProjectForward(sectionCenter, toolHeading, lookAheadOnDistance);

var coverage = _coverageMapService.GetSegmentCoverage(
    lookAheadCenter,
    toolHeading,
    halfWidth);

bool lookOnCovered = coverage.CoveragePercent >= COVERAGE_OVERLAP_THRESHOLD;
```

### Phase 5: Boundary Segment Checks (Optional)

Apply same transform approach to boundary detection:

```csharp
// Update: Shared/AgValoniaGPS.Services/Interfaces/IBoundaryService.cs

BoundaryResult GetSegmentBoundaryStatus(Vec2 sectionCenter, double heading, double halfWidth);
```

This detects when section edges cross boundaries even if center is inside.

---

## Testing Strategy

### Unit Tests
1. **Transform correctness**: Verify ToLocalCoords produces expected results
2. **Y-intercept calculation**: Test edge cases (horizontal, vertical, on-axis edges)
3. **Interval merging**: Overlapping, adjacent, and gapped intervals
4. **Known geometry**: Hand-calculated triangle-section overlaps

### Integration Tests
1. **Comparison mode**: Run both point and segment methods, compare results
2. **Gap detection**: Verify gaps between patches are detected
3. **Edge straddling**: Section endpoints on different patches
4. **Performance**: Measure time per check with realistic patch counts

### Visual Testing
1. **Debug overlay**: Draw coverage intervals on map
2. **Side-by-side**: Show point result vs segment result
3. **Highlight discrepancies**: Where methods disagree

---

## Migration Strategy

### Phase A: Parallel Implementation
1. Implement segment method alongside point method
2. Add debug logging to compare results
3. No behavior change - just data collection

### Phase B: Gradual Rollout
1. Add configuration toggle: `UseSegmentBasedCoverage`
2. Default off, allow testing
3. Collect feedback on accuracy

### Phase C: Default Enable
1. Make segment-based the default
2. Keep point-based as fallback option
3. Deprecate point-based after validation

---

## Files to Modify/Create

| File | Action | Description |
|------|--------|-------------|
| `Models/Coverage/CoverageResult.cs` | CREATE | Result record |
| `Models/Boundary/BoundaryResult.cs` | CREATE | Result record |
| `Models/Base/GeometryMath.cs` | MODIFY | Add transform utilities |
| `Services/Interfaces/ICoverageMapService.cs` | MODIFY | Add segment method |
| `Services/Coverage/CoverageMapService.cs` | MODIFY | Implement segment coverage |
| `Services/Section/SectionControlService.cs` | MODIFY | Use segment checks |
| `Services/Interfaces/ISectionControlService.cs` | MODIFY | Add overlap threshold config |

---

## Performance Considerations

1. **Frustum culling**: Reject distant patches before transform (~90% reduction)
2. **Sign check early-exit**: Skip triangles entirely above/below axis
3. **Batch processing**: Check all sections in one patch loop
4. **Precompute sin/cos**: One calculation per section, not per triangle
5. **Interval list pooling**: Reuse lists to reduce allocations

Expected performance: <1ms for 16 sections with 1000+ coverage triangles.

---

## Future Enhancements

### Tram Line Detection
Same transform approach could detect:
- Distance from wheel tracks
- Whether section overlaps tram line
- Controlled traffic farming support

### Headland Segment Checks
Detect when section partially enters headland zone, not just center point.

### Coverage Quality Metrics
- Track overlap percentage history
- Detect consistently missed strips
- Suggest guidance line adjustments

---

## References

- Brian's feedback on points vs segments (AgOpenGPS architectural insight)
- Qt developer's transform-based intersection approach
- Current implementation: `CoverageMapService.IsPointCovered()`
- Current section control: `SectionControlService.Update()`

---

## Checklist

### Phase 1: Data Structures
- [ ] Create CoverageResult record
- [ ] Create BoundaryResult record

### Phase 2: Geometry Utilities
- [ ] Add ToLocalCoords to GeometryMath
- [ ] Add GetXInterceptAtYZero to GeometryMath
- [ ] Add interval merging utility

### Phase 3: Coverage Service
- [ ] Add GetSegmentCoverage to interface
- [ ] Implement GetSegmentCoverage
- [ ] Add frustum culling
- [ ] Unit tests for coverage calculation

### Phase 4: Section Control Integration
- [ ] Add overlap threshold configuration
- [ ] Update SectionControlService to use segment checks
- [ ] Add comparison logging (debug)
- [ ] Integration tests

### Phase 5: Boundary Checks (Optional)
- [ ] Add GetSegmentBoundaryStatus to interface
- [ ] Implement boundary segment checks
- [ ] Update section control to use segment boundary checks

### Phase 6: Migration
- [ ] Add UseSegmentBasedCoverage toggle
- [ ] Test in parallel mode
- [ ] Enable by default
- [ ] Remove/deprecate point-based method
