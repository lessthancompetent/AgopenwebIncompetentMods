// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;

namespace AgOpenWeb.Services.Interfaces;

/// <summary>
/// Terrain: per-field elevation map built from RTK altitude samples while
/// driving. A sparse 5 m grid of running-mean altitudes keyed by field-local
/// cell coordinates, persisted per field (elevation.json) and served to the
/// web map's Terrain shading overlay. Distinct from IElevationLogService
/// (the legacy Elevation.txt CSV point log).
/// </summary>
public interface IElevationMapService
{
    /// <summary>Grid cell size in metres (5 m).</summary>
    double CellSizeM { get; }

    /// <summary>Number of cells holding at least one sample.</summary>
    int CellCount { get; }

    /// <summary>Min/max recorded altitude in metres, or null with no samples.</summary>
    (double MinM, double MaxM)? AltitudeRange { get; }

    /// <summary>
    /// Fold one altitude sample into the grid cell containing (easting,
    /// northing). Running mean per cell — call rate / throttling is the
    /// caller's concern. Thread-safe.
    /// </summary>
    void Record(double easting, double northing, double altitudeM);

    /// <summary>Snapshot of all cells as (cellCentreE, cellCentreN, meanAltM)
    /// in field-local metres.</summary>
    IReadOnlyList<(double CellE, double CellN, double AltM)> GetCells();

    /// <summary>Drop all samples (field close).</summary>
    void Clear();

    /// <summary>
    /// Persist the grid to &lt;fieldDirectory&gt;/elevation.json. No-op when
    /// nothing changed since the last save/load, or when the grid is empty
    /// (never clobbers an existing file with nothing).
    /// </summary>
    void SaveToFile(string fieldDirectory);

    /// <summary>Replace the grid from &lt;fieldDirectory&gt;/elevation.json
    /// (empty grid when the file is absent or unreadable).</summary>
    void LoadFromFile(string fieldDirectory);

    /// <summary>
    /// Web payload for GET /api/elevation:
    /// {"cell":5,"min":…,"max":…,"cells":[[e,n,alt],…]} with e/n the cell
    /// centres in field-local metres and alt to 0.1 m. "{}" when empty.
    /// </summary>
    string BuildElevationJson();

    /// <summary>
    /// GeoJSON FeatureCollection of Point features (properties {alt}) for the
    /// off-board viewer, converted to WGS84 around the field origin. Null when
    /// the grid is empty.
    /// </summary>
    string? BuildGeoJson(double originLat, double originLon);
}
