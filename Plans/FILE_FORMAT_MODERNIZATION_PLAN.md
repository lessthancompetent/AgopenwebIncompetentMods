# File Format Modernization Plan

## Overview

Modernize AgValoniaGPS file formats from legacy AgOpenGPS text/XML formats to unified JSON, improving maintainability, flexibility, and developer experience while providing one-way import from legacy formats.

## Design Philosophy

> AgValoniaGPS may use different/improved formats from AgOpenGPS when it benefits code simplicity or features. Provide one-way import from AgOpenGPS formats rather than maintaining full backwards compatibility.

---

## Current State Analysis

### Fields (Legacy Text Files)

| File | Format | Purpose |
|------|--------|---------|
| `Field.txt` | Fixed-line text | Origin coordinates, convergence, timestamp |
| `Boundary.txt` | Text with point lists | Outer boundary + inner holes |
| `Headland.Txt` | Text with point lists | Headland boundary |
| `TrackLines.txt` | Multi-line text blocks | AB lines and curves |
| `BackPic.Txt` | Text | Background image bounds |
| `BackPic.png` | Binary | Satellite/aerial image |

**Issues:**
- Fixed-line parsing (fragile, order-dependent)
- No schema versioning
- Angles stored in radians, displayed in degrees
- Duplicate `isDriveThru` line handling (legacy quirk)
- No graceful handling of unknown fields

### Vehicle Profiles (XML)

| File | Format | Purpose |
|------|--------|---------|
| `*.XML` | AgOpenGPS XML | Vehicle + tool + guidance configuration |

**Issues:**
- 100+ flat settings (no hierarchy)
- 17 hardcoded section positions
- String-based booleans ("True"/"False")
- Mixed concerns (simulator settings in vehicle config)
- Magic setting names scattered across codebase

### Application Settings (JSON - Already Modern)

| File | Format | Purpose |
|------|--------|---------|
| `appsettings.json` | JSON | Window state, NTRIP, paths, UI preferences |

**Status:** Already using modern format. No changes needed.

### Models with Duplication

| Model | Status | Replacement |
|-------|--------|-------------|
| `ABLine` | Marked `[Obsolete]` | `Track` (unified) |
| `VehicleProfile` | Marked `[Obsolete]` | `ConfigurationStore` serialization |

---

## Proposed New Formats

### 1. Unified Field Format (`field.json`)

Consolidate all field data into a single JSON file:

```json
{
  "version": "1.0",
  "name": "MyField",
  "createdDate": "2025-12-19T10:30:00Z",
  "lastModifiedDate": "2025-12-19T10:30:00Z",

  "origin": {
    "latitude": 40.7128,
    "longitude": -74.0060,
    "altitude": 0
  },

  "localCoordinates": {
    "convergence": 0.5,
    "offsetX": 0,
    "offsetY": 0
  },

  "boundary": {
    "outer": [
      { "easting": 0, "northing": 0, "heading": 0 },
      { "easting": 100, "northing": 0, "heading": 90 },
      { "easting": 100, "northing": 100, "heading": 180 },
      { "easting": 0, "northing": 100, "heading": 270 }
    ],
    "inner": [
      {
        "isDriveThrough": false,
        "points": [...]
      }
    ]
  },

  "headland": {
    "distance": 15.0,
    "points": [...]
  },

  "tracks": [
    {
      "name": "AB Line 1",
      "points": [
        { "easting": 0, "northing": 0, "heading": 0 },
        { "easting": 100, "northing": 100, "heading": 45 }
      ],
      "nudgeDistance": 0,
      "isVisible": true,
      "isActive": false
    },
    {
      "name": "Curve 1",
      "points": [...],
      "nudgeDistance": 0,
      "isVisible": true,
      "isActive": false
    }
  ],

  "backgroundImage": {
    "enabled": true,
    "filename": "background.png",
    "bounds": {
      "minEasting": 0,
      "maxEasting": 500,
      "minNorthing": 0,
      "maxNorthing": 500
    }
  }
}
```

**Key improvements:**
- Single file instead of 5+ separate files
- All angles in degrees (human-readable)
- Schema version for future migrations
- Unified track format (no ABLine vs Curve distinction in storage)
- Optional fields supported naturally

**Directory structure:**
```
Fields/
├── MyField/
│   ├── field.json        # All metadata, boundaries, tracks
│   └── background.png    # Optional satellite image
```

### 2. Vehicle/Tool Profile Format (`profile.json`)

Hierarchical JSON replacing flat XML:

```json
{
  "version": "1.0",
  "name": "John Deere 5055E",
  "createdDate": "2025-12-19T10:30:00Z",

  "vehicle": {
    "type": "tractor",
    "wheelbase": 2.5,
    "trackWidth": 1.8,
    "antenna": {
      "height": 3.0,
      "pivot": 0,
      "offset": 0
    },
    "steering": {
      "maxAngle": 35.0,
      "maxAngularVelocity": 35.0
    }
  },

  "tool": {
    "type": "sprayer",
    "width": 6.0,
    "overlap": 0.05,
    "offset": 0,
    "hitch": {
      "length": -1.8,
      "isTrailing": true,
      "isTBT": false
    },
    "sections": [
      { "position": -2.5, "width": 1.0 },
      { "position": -1.5, "width": 1.0 },
      { "position": -0.5, "width": 1.0 },
      { "position": 0.5, "width": 1.0 },
      { "position": 1.5, "width": 1.0 },
      { "position": 2.5, "width": 1.0 }
    ],
    "timing": {
      "lookAheadOn": 0,
      "lookAheadOff": 0,
      "turnOffDelay": 0
    }
  },

  "guidance": {
    "algorithm": "purePursuit",
    "goalPointLookAhead": 4.0,
    "goalPointLookAheadMult": 1.4,
    "goalPointAcquireFactor": 1.5,
    "stanley": {
      "distanceErrorGain": 0.8,
      "headingErrorGain": 1.0,
      "integralGain": 0.0
    },
    "purePursuit": {
      "integralGain": 0.0
    },
    "uTurn": {
      "radius": 8.0,
      "extensionLength": 20.0,
      "compensation": 1.0
    }
  },

  "simulator": {
    "latitude": 32.59,
    "longitude": -87.18
  }
}
```

**Key improvements:**
- Hierarchical structure (vehicle → tool → guidance)
- Dynamic sections array (no 17-element limit)
- Section width per section (not just position)
- Clear separation of concerns
- Tool type field for future implement-specific logic

---

## Implementation Phases

### Phase 1: JSON Infrastructure

1. Create `FieldJsonService` for new field format
2. Create `ProfileJsonService` for new profile format
3. Add JSON schema files for validation (optional)
4. Keep legacy services unchanged

**Files to create:**
- `Shared/AgValoniaGPS.Services/Field/FieldJsonService.cs`
- `Shared/AgValoniaGPS.Services/Profile/ProfileJsonService.cs`
- `Shared/AgValoniaGPS.Models/Field/FieldData.cs` (unified model)

### Phase 2: Auto-Detection & Import

1. On field load: detect format (JSON vs legacy text)
2. If legacy: import and convert to JSON
3. Save only in new JSON format
4. Same for profiles: detect XML vs JSON

**Detection logic:**
```csharp
public async Task<Field> LoadField(string path)
{
    var jsonPath = Path.Combine(path, "field.json");
    if (File.Exists(jsonPath))
    {
        return await LoadJsonField(jsonPath);
    }

    // Legacy import
    var legacyField = await LoadLegacyField(path);
    await SaveJsonField(legacyField, jsonPath);
    return legacyField;
}
```

### Phase 3: Model Consolidation

1. Remove `ABLine` class entirely (use `Track`)
2. Serialize `ConfigurationStore` directly for profiles
3. Update all references

### Phase 4: Cleanup (Optional)

1. Remove legacy file services
2. Remove obsolete model classes
3. Delete legacy format tests

---

## Migration Strategy

### For End Users

- **Transparent migration**: App detects legacy files and converts automatically
- **No data loss**: All legacy data preserved in new format
- **One-way**: Once converted, fields use new format only
- **Backup recommended**: Users should backup before first run of new version

### For Developers

- **Gradual transition**: Both formats supported during transition
- **Feature flags**: Can toggle new format on/off during development
- **Test coverage**: Unit tests for both legacy import and new format

---

## Compatibility Notes

### AgOpenGPS Interoperability

After migration, AgValoniaGPS fields will NOT be directly readable by AgOpenGPS. Options:

1. **Export feature**: Add "Export to AgOpenGPS format" menu option
2. **Standalone converter**: Small utility to convert JSON → legacy text
3. **Documentation**: Clear notes that formats are incompatible

### Sharing Between AgValoniaGPS Users

New JSON format is the standard for sharing between AgValoniaGPS installations.

---

## File Size Comparison (Estimated)

| Data | Legacy | JSON | Change |
|------|--------|------|--------|
| Simple field (boundary + 1 track) | ~2 KB (5 files) | ~1.5 KB (1 file) | -25% |
| Complex field (100 tracks) | ~50 KB (5 files) | ~45 KB (1 file) | -10% |
| Vehicle profile | ~15 KB (XML) | ~3 KB (JSON) | -80% |

JSON is more compact than XML due to less verbose syntax.

---

## Success Criteria

- [ ] Fields load/save in JSON format
- [ ] Legacy AgOpenGPS fields import correctly
- [ ] Vehicle profiles load/save in JSON format
- [ ] Legacy XML profiles import correctly
- [ ] No 17-section limit
- [ ] All angles stored in degrees
- [ ] Schema version field present for future migrations
- [ ] Unit tests pass for both formats

---

## Open Questions

1. **Background images**: Keep as separate PNG or embed as base64 in JSON?
   - Recommendation: Keep separate (large files shouldn't bloat JSON)

2. **Field sharing**: Support compressed `.agfield` package (zip)?
   - Could bundle `field.json` + `background.png` for easy sharing

3. **Schema validation**: Use JSON Schema for validation?
   - Nice to have, not required for MVP

4. **Profile inheritance**: Allow profiles to extend base profiles?
   - Future enhancement, not in initial scope
