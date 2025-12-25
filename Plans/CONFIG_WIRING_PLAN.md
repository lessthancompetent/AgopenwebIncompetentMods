# Configuration Wiring Plan

This document tracks the wiring of UI configuration settings to AgValonia backend services.

## UI Reorganization Notes

**Data I/O Dialog Reorganization** (Planned):
- Current `DataIODialogPanel` will be split:
  - UDP Communication + Module Connections → **Module Monitoring Panel** (status bar access)
  - GPS Data → **GPS Data Panel** (status bar access)
  - NTRIP Configuration → **Data Sources Tab** in config dialog (implemented)
- Status bar will provide quick access to monitoring panels via clickable indicators

**Status Bar** (Planned):
- RTK Fix indicator → opens NTRIP quick panel / reconnect
- Module status → opens Module Monitoring panel
- GPS quality/HDOP → opens GPS Data panel
- Area worked → opens stats

## Architecture Overview

All configuration flows through **ConfigurationStore** (singleton):
```
ConfigurationStore.Instance
├── Vehicle      (VehicleConfig)
├── Tool         (ToolConfig)
├── Guidance     (GuidanceConfig)
├── Display      (DisplayConfig)
├── Connection   (ConnectionConfig)
├── Machine      (MachineConfig)
├── Ahrs         (AhrsConfig)
└── Simulator    (SimulatorConfig)
```

Services access configuration via `ConfigurationStore.Instance.SubConfig.Property`.

---

## Tab-by-Tab Wiring Checklist

### 1. Vehicle Tab → VehicleConfig
**File**: `VehicleConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Vehicle Type | `Vehicle.Type` | Diagram display | ⬜ |
| Wheelbase | `Vehicle.Wheelbase` | TrackGuidanceService, YouTurnGuidanceService | ⬜ |
| Track Width | `Vehicle.TrackWidth` | Geometry calculations | ⬜ |
| Antenna Height | `Vehicle.AntennaHeight` | GPS offset corrections | ⬜ |
| Antenna Pivot | `Vehicle.AntennaPivot` | GPS position projection | ⬜ |
| Antenna Offset | `Vehicle.AntennaOffset` | GPS lateral correction | ⬜ |
| Max Steer Angle | `Vehicle.MaxSteerAngle` | TrackGuidanceService steering limits | ⬜ |
| Max Angular Velocity | `Vehicle.MaxAngularVelocity` | Yaw rate limiting | ⬜ |

**Wiring Notes**:
- Vehicle dimensions affect steering geometry in TrackGuidanceService
- Antenna offsets used in GPS position projection (local plane calculations)
- Wheelbase directly affects minimum turning radius

---

### 2. Tool Tab → ToolConfig
**File**: `ToolConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Tool Width | `Tool.Width` | Section control, Tramline | ⬜ |
| Overlap | `Tool.Overlap` | Section overlap compensation | ⬜ |
| Lateral Offset | `Tool.Offset` | Tool lateral positioning | ⬜ |
| Tool Type (4 modes) | `Tool.IsToolTrailing`, etc. | Hitch geometry | ⬜ |
| Hitch Length | `Tool.HitchLength` | Tool position tracking | ⬜ |
| Trailing Hitch | `Tool.TrailingHitchLength` | TBT tool geometry | ⬜ |
| Look Ahead On | `Tool.LookAheadOnSetting` | Section auto-on distance | ⬜ |
| Look Ahead Off | `Tool.LookAheadOffSetting` | Section auto-off distance | ⬜ |
| Turn Off Delay | `Tool.TurnOffDelay` | Section shutoff timing | ⬜ |
| Number of Sections | `NumSections` | Section control | ⬜ |
| Section Widths | `Tool.SectionWidths[]` | Individual section sizes | ⬜ |
| Zone Ranges | `Tool.ZoneRanges[]` | Zone grouping | ⬜ |

**Wiring Notes**:
- Tool width is critical for tramline spacing calculations
- Section widths stored as centimeters (int array, 16 elements)
- Look ahead settings affect section on/off timing based on speed

---

### 3. U-Turn Tab → GuidanceConfig
**File**: `UTurnConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Turn Radius | `Guidance.UTurnRadius` | YouTurnGuidanceService | ⬜ |
| Extension Length | `Guidance.UTurnExtension` | Entry/exit leg length | ⬜ |
| Distance from Boundary | `Guidance.UTurnDistanceFromBoundary` | YouTurnCreationService | ⬜ |
| U-Turn Style | `Guidance.UTurnStyle` | Path generation (0=normal, 1=K) | ⬜ |
| Smoothing | `Guidance.UTurnSmoothing` | Spline smoothing (1-50) | ⬜ |
| Compensation | `Guidance.UTurnCompensation` | Steering compensation | ⬜ |
| Skip Width | `Guidance.UTurnSkipWidth` | Row skip on return | ⬜ |

**Wiring Notes**:
- U-turn radius should default to 2x wheelbase minimum
- Smoothing affects path curvature continuity
- Skip width used for pattern skip (e.g., skip 1 row on wide implements)

---

### 4. Machine Control Tab → MachineConfig
**Files**: `MachineControlConfigTab.axaml`, `MachineModuleSubTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Machine Module On/Off | `Machine.MachineModuleEnabled` | UDP module comm | ⬜ |
| Raise Time | `Machine.RaiseTime` | Hydraulic timing | ⬜ |
| Lower Time | `Machine.LowerTime` | Hydraulic timing | ⬜ |
| Look Ahead | `Machine.LookAhead` | Lift trigger distance | ⬜ |
| Invert Relay | `Machine.InvertRelay` | Relay logic | ⬜ |
| Pin Assignments (24) | `Machine.PinAssignments[]` | Relay control | ⬜ |
| User Values (1-4) | `Machine.User1Value`, etc. | Custom module data | ⬜ |
| Alarm Stops AutoSteer | `Ahrs.AlarmStopsAutoSteer` | AutoSteerService | ⬜ |

**Wiring Notes**:
- Pin assignments map GPIO pins to functions (sections, hydraulics, tram)
- Hydraulic timing in seconds (0.1 resolution)
- User values sent to modules for custom implementations

---

### 5. Tram Lines Tab → GuidanceConfig
**File**: `TramConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Tram Lines Enabled | `Guidance.TramLinesEnabled` | TramlineService | ⬜ |
| Tram Line Style | `Guidance.TramLineStyle` | Rendering style | ⬜ |
| Tram Passes | `Guidance.TramPasses` | Pass count between trams | ⬜ |
| Seed Tram | `Guidance.SeedTram` | Seed drill mode | ⬜ |
| Half Width Mode | `Guidance.TramHalfWidth` | Half-width tram mode | ⬜ |
| Outer Tram | `Guidance.TramOuter` | Outer tram offset | ⬜ |

**Wiring Notes**:
- Tram passes = number of passes between tramlines
- Half-width mode for implements narrower than vehicle

---

### 6. Data Sources Tab → ConnectionConfig
**Files**: `SourcesConfigTab.axaml`, `GpsSubTab.axaml`, `NtripSubTab.axaml`, `RollSubTab.axaml`

#### GPS Settings (GpsSubTab)
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Dual GPS Mode | `Connection.IsDualGps` | GpsService, heading calc | ⬜ |
| GPS Update Rate | `Connection.GpsUpdateRate` | NMEA parse rate | ⬜ |
| Min Fix Quality | `Connection.MinFixQuality` | Fix quality filter | ⬜ |
| Dual Heading Offset | `Connection.DualHeadingOffset` | Dual antenna heading | ⬜ |
| Dual Reverse Distance | `Connection.DualReverseDistance` | Reverse detection | ⬜ |
| Single Min Step | `Connection.MinGpsStep` | Min movement threshold | ⬜ |
| Fix-to-Fix Distance | `Connection.FixToFixDistance` | Position jump filter | ⬜ |
| Heading Fusion Weight | `Connection.HeadingFusionWeight` | GPS/IMU blend | ⬜ |

#### NTRIP Settings (NtripSubTab) ✅ UI Complete
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Caster Host | `Connection.NtripCasterHost` | NtripClientService | ✅ UI |
| Caster Port | `Connection.NtripCasterPort` | NtripClientService | ✅ UI |
| Mount Point | `Connection.NtripMountPoint` | NtripClientService | ✅ UI |
| Username | `Connection.NtripUsername` | NtripClientService | ✅ UI |
| Password | `Connection.NtripPassword` | NtripClientService | ✅ UI |
| Auto Connect | `Connection.NtripAutoConnect` | App startup | ✅ UI |

#### RTK Monitoring
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| RTK Lost Alarm | `Connection.RtkLostAlarm` | Alert system | ⬜ |
| RTK Lost Action | `Connection.RtkLostAction` | AutoSteerService | ⬜ |
| Max Differential Age | `Connection.MaxDifferentialAge` | RTK quality check | ⬜ |
| Max HDOP | `Connection.MaxHdop` | Position quality filter | ⬜ |

#### AgShare Cloud
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| AgShare Server | `Connection.AgShareServer` | Cloud sync | ⬜ |
| AgShare API Key | `Connection.AgShareApiKey` | Authentication | ⬜ |
| AgShare Enabled | `Connection.AgShareEnabled` | Cloud sync toggle | ⬜ |

**Wiring Notes**:
- NTRIP settings build `NtripConfiguration` object for service
- RTK Lost Action: 0=Warn only, 1=Pause steering, 2=Stop steering
- GPS update rate affects guidance responsiveness

---

### 7. Display Tab → DisplayConfig
**File**: `DisplayConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Polygons Visible | `Display.PolygonsVisible` | Map rendering | ⬜ |
| Speedometer Visible | `Display.SpeedometerVisible` | UI overlay | ⬜ |
| Keyboard Enabled | `Display.KeyboardEnabled` | Input handling | ⬜ |
| Headland Distance | `Display.HeadlandDistanceVisible` | UI overlay | ⬜ |
| Auto Day/Night | `Display.AutoDayNight` | Time-based theme | ⬜ |
| Svenn Arrow | `Display.SvennArrowVisible` | Map rendering | ⬜ |
| Start Fullscreen | `Display.StartFullscreen` | Window manager | ⬜ |
| Elevation Log | `Display.ElevationLogEnabled` | Data logging | ⬜ |
| Field Texture | `Display.FieldTextureVisible` | Map rendering | ⬜ |
| Grid Visible | `Display.GridVisible` | Map rendering | ⬜ |
| Extra Guidelines | `Display.ExtraGuidelines` | Map rendering | ⬜ |
| Guidelines Count | `Display.ExtraGuidelinesCount` | Map rendering | ⬜ |
| Line Smooth | `Display.LineSmoothEnabled` | Map rendering | ⬜ |
| Direction Markers | `Display.DirectionMarkersVisible` | Map rendering | ⬜ |
| Section Lines | `Display.SectionLinesVisible` | Map rendering | ⬜ |
| Units (Metric/Imperial) | `IsMetric` | All display conversions | ⬜ |

**Wiring Notes**:
- Display settings affect DrawingContextMapControl rendering
- Grid visibility fires `GridVisibilityChanged` event
- Day/night mode affects color scheme throughout app

---

### 8. Additional Options Tab → DisplayConfig, AhrsConfig
**File**: `AdditionalOptionsConfigTab.axaml`

#### Screen Buttons
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| U-Turn Button | `Display.UTurnButtonVisible` | UI visibility | ⬜ |
| Lateral Button | `Display.LateralButtonVisible` | UI visibility | ⬜ |

#### Sounds
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Auto Steer Sound | `Display.AutoSteerSound` | Audio service | ⬜ |
| U-Turn Sound | `Display.UTurnSound` | Audio service | ⬜ |
| Hydraulic Sound | `Display.HydraulicSound` | Audio service | ⬜ |
| Sections Sound | `Display.SectionsSound` | Audio service | ⬜ |

#### Hardware
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Hardware Messages | `Display.HardwareMessagesEnabled` | Status display | ⬜ |

**Wiring Notes**:
- Sounds require audio playback service (not yet implemented)
- Button visibility controls what appears in main UI panels

---

## Implementation Priority

### Phase 1: Core Guidance (Critical for field operation)
1. ⬜ Vehicle Tab → VehicleConfig (wheelbase, antenna)
2. ⬜ Tool Tab → ToolConfig (width, sections)
3. ⬜ U-Turn Tab → GuidanceConfig (turn parameters)

### Phase 2: Data Sources (Required for GPS/RTK)
4. ⬜ Data Sources Tab → ConnectionConfig (GPS mode, NTRIP)

### Phase 3: Machine Control (Hardware integration)
5. ⬜ Machine Control Tab → MachineConfig (relays, hydraulics)
6. ⬜ Tram Lines Tab → GuidanceConfig (tramline settings)

### Phase 4: Display & Polish
7. ⬜ Display Tab → DisplayConfig (visual settings)
8. ⬜ Additional Options Tab → DisplayConfig (sounds, buttons)

---

## Wiring Pattern

For each setting, the wiring involves:

### 1. ViewModel Property
Ensure ConfigurationViewModel has accessor:
```csharp
// Direct access via ConfigurationStore
public VehicleConfig Vehicle => ConfigurationStore.Instance.Vehicle;
public double Wheelbase => Vehicle.Wheelbase;
```

### 2. XAML Binding
Bind control to property with numeric input support:
```xml
<Button Command="{Binding OpenNumericInputCommand}"
        CommandParameter="Vehicle.Wheelbase|Wheelbase|m|0.5|10|2"/>
```

### 3. Service Access
Services read from ConfigurationStore:
```csharp
var wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
```

### 4. Profile Persistence
VehicleProfileService saves/loads config:
```csharp
profile.Vehicle.Wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
```

---

## Current State Summary

| Tab | UI Complete | Bindings | Services | Profile Save |
|-----|-------------|----------|----------|--------------|
| Vehicle | ✅ | ⬜ | ⬜ | ⬜ |
| Tool | ✅ | ⬜ | ⬜ | ⬜ |
| U-Turn | ✅ | ⬜ | ⬜ | ⬜ |
| Machine Control | ✅ | ⬜ | ⬜ | ⬜ |
| Tram Lines | ✅ | ⬜ | ⬜ | ⬜ |
| Data Sources | ✅ | ⬜ | ⬜ | ⬜ |
| Display | ✅ | ⬜ | ⬜ | ⬜ |
| Additional Options | ✅ | ⬜ | ⬜ | ⬜ |

**Legend**: ✅ Complete | ⬜ Not Started | 🔄 In Progress

---

## Notes

- All config models use ReactiveUI (`RaiseAndSetIfChanged`) for automatic UI updates
- Services access config via `ConfigurationStore.Instance` singleton
- Profile persistence uses XML format via `VehicleProfileService`
- Some services may need migration from direct property access to ConfigurationStore
