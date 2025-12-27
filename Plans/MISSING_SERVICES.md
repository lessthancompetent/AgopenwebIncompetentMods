# Missing Services from AgOpenGPS

This document tracks services and features from the original AgOpenGPS that were not extracted into AgOpenGPS.Core and need to be ported to AgValoniaGPS3.

## 1. BoundaryBuilder Service (Build Boundary From Tracks)

**Original Location:** `/SourceCodeLatest/GPS/Classes/BoundaryBuilder.cs`

**Purpose:** Creates a field boundary polygon from AB lines and curve tracks by finding their intersections and connecting them.

**How it works:**
1. Takes AB lines and/or curve guidance tracks as input
2. Extends track endpoints to ensure they intersect with other tracks
3. Finds intersection points between different tracks
4. Trims tracks to their intersection points
5. Connects trimmed segments into a closed polygon boundary
6. Saves the boundary to Boundary.txt

**Key Components:**
- `BoundaryBuilder` class with:
  - `SetTracks(List<CTrk> tracks)` - Input tracks
  - `ExtendAllTracks(double extendMeters)` - Auto-extend to find intersections
  - `BuildSegments()` - Convert tracks to line segments
  - `FindIntersections()` - Find where tracks cross
  - `TrimSegmentsToIntersections()` - Cut tracks at intersection points
  - `BuildTrimmedBoundary()` - Create final polygon
  - `SaveToBoundaryFile(string fieldDirectory)` - Persist to file

**Dependencies:**
- `CTrk` - Track model (AB line or Curve)
- `TrackMode` enum (AB, Curve)
- `vec2`, `vec3` - Vector types
- `CBoundaryList` - Boundary polygon model
- Track file loading (`FileLoadTracks()`)

**UI Requirements:**
- Track selection list with checkboxes
- Preview map showing tracks and generated boundary
- Track adjustment controls (extend/shrink endpoints)
- "Auto-find intersections" button
- "Build Boundary" and "Save" buttons

**Source Files to Reference:**
- `/SourceCodeLatest/GPS/Classes/BoundaryBuilder.cs` - Core logic
- `/SourceCodeLatest/GPS/Forms/Field/FormBuildBoundaryFromTracks.cs` - UI/Form
- `/AgValoniaGPS/AgValoniaGPS.ViewModels/Dialogs/FieldManagement/BuildBoundaryFromTracksViewModel.cs` - Partial Avalonia port (stub)

**Priority:** Medium - Useful for creating boundaries from guidance lines

---

## 2. ToolPositionService (Config Settings Support)

**Status:** 📋 Detailed plan created - see `Plans/SERVICE_PLAN_ToolPositionService.md`

**Purpose:** Calculate tool/implement position relative to vehicle pivot point based on hitch configuration.

**Priority:** Medium - Needed for accurate coverage mapping and section control

---

## 3. SectionControlService (Config Settings Support)

**Status:** 📋 Detailed plan created - see `Plans/SERVICE_PLAN_SectionControlService.md`

**Purpose:** Manage automatic section on/off based on coverage, boundaries, and headlands.

**Priority:** Medium-High - Core functionality for sprayers/planters

---

## 4. CoverageMapService (Config Settings Support)

**Status:** 📋 Detailed plan created - see `Plans/SERVICE_PLAN_CoverageMapService.md`

**Purpose:** Track and render coverage (where tool has been active).

**Priority:** Medium - Needed for visual feedback and section control

---

## 5. AntennaTransformService (Config Settings Support) ✅ COMPLETE

**Status:** Implemented in `GpsService.TransformAntennaToPivot()` (December 2024)

**Implementation:**
- Integrated directly into GpsService rather than a separate service
- `TransformAntennaToPivot()` method applies both fore/aft (AntennaPivot) and lateral (AntennaOffset) offsets
- Sign conventions: Negative offset = antenna LEFT of center, Positive = RIGHT
- All guidance calculations now use the transformed pivot position

**Config Settings Used:**
- `Vehicle.AntennaPivot` - Distance from antenna to pivot (along centerline)
- `Vehicle.AntennaOffset` - Lateral offset of antenna from centerline
- `Vehicle.AntennaHeight` - NOT USED (terrain compensation - low priority)

---

## 6. TramLineService (Config Settings Support)

**Status:** 📋 Detailed plan created - see `Plans/SERVICE_PLAN_TramLineService.md`

**Purpose:** Generate and display tram lines for controlled traffic farming.

**Priority:** Low - Advanced feature

---

## Notes

- Services should be added to `Shared/AgValoniaGPS.Services/`
- Interfaces should be in `Shared/AgValoniaGPS.Services/Interfaces/`
- Models should be in `Shared/AgValoniaGPS.Models/`
- UI panels should be in `Shared/AgValoniaGPS.Views/Controls/Dialogs/` or `Controls/Panels/`
