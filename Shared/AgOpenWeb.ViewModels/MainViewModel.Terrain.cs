// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Terrain: while a field is open, fold the RTK altitude into the per-field
// elevation grid (IElevationMapService, 5 m cells) as the vehicle drives.
// Recording is automatic — no operator action; the web map's Terrain toggle
// (Screen & Alerts → Map Background) shades the result via /api/elevation.
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;

namespace AgOpenWeb.ViewModels;

public partial class MainViewModel
{
    // Sample gates: skip when stationary (GPS altitude noise would pile into
    // one cell), when altitude is the no-fix/simulator 0 sentinel, and until
    // the vehicle has moved ~2 m since the last accepted sample.
    private const double TerrainMinSpeedMps = 0.3;
    private const double TerrainSampleSpacingM = 2.0;

    private double _terrainLastE = double.NaN;
    private double _terrainLastN = double.NaN;

    /// <summary>
    /// Called from the GPS handler each fix (10 Hz). Cheap: three compares and
    /// a distance check before anything is recorded.
    /// </summary>
    private void RecordTerrainSample(double easting, double northing, Models.Position position)
    {
        if (!IsFieldOpen) return;
        if (Math.Abs(position.Speed) < TerrainMinSpeedMps) return;
        if (position.Altitude == 0) return; // altitude-less fix (simulator / bare parsers)

        if (!double.IsNaN(_terrainLastE))
        {
            double dx = easting - _terrainLastE;
            double dy = northing - _terrainLastN;
            if (dx * dx + dy * dy < TerrainSampleSpacingM * TerrainSampleSpacingM)
                return;
        }

        _terrainLastE = easting;
        _terrainLastN = northing;
        _elevationMapService.Record(easting, northing, position.Altitude);
    }

    /// <summary>Drop the grid + throttle anchor on field close (ClearFieldState).</summary>
    private void ResetTerrainState()
    {
        _elevationMapService.Clear();
        _terrainLastE = double.NaN;
        _terrainLastN = double.NaN;
    }
}
