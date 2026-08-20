// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Terrain: sparse per-field elevation grid. RTK altitude is cm-grade, so the
// running mean of the samples falling in each 5 m cell gives a stable field
// elevation model the operator builds for free just by driving. Persisted as
// elevation.json in the field directory; the web map shades it via
// /api/elevation and the off-board viewer gets elevation.geojson.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Services.Interfaces;

namespace AgOpenWeb.Services.Elevation;

public class ElevationMapService : IElevationMapService
{
    private const double CELL_SIZE = 5.0; // metres — coarse enough to average GPS noise, fine enough for drainage planning
    private const string FILE_NAME = "elevation.json";

    // Same locking model as CoverageMapService: samples arrive from the GPS
    // handler while the web server thread reads snapshots.
    private readonly object _lock = new();

    // (cellE, cellN) → running mean + sample count. Cell keys are
    // Floor(coord / CELL_SIZE) so negative field-local coords work.
    private readonly Dictionary<(int E, int N), (double Mean, int Count)> _cells = new();

    private double _minAlt = double.MaxValue;
    private double _maxAlt = double.MinValue;
    private bool _dirty;

    public double CellSizeM => CELL_SIZE;

    public int CellCount { get { lock (_lock) return _cells.Count; } }

    public (double MinM, double MaxM)? AltitudeRange
    {
        get { lock (_lock) return _cells.Count > 0 ? (_minAlt, _maxAlt) : null; }
    }

    public double? GetAltitude(double easting, double northing)
    {
        var key = ((int)Math.Floor(easting / CELL_SIZE), (int)Math.Floor(northing / CELL_SIZE));
        lock (_lock)
            return _cells.TryGetValue(key, out var cell) ? cell.Mean : null;
    }

    public void Record(double easting, double northing, double altitudeM)
    {
        var key = ((int)Math.Floor(easting / CELL_SIZE), (int)Math.Floor(northing / CELL_SIZE));
        lock (_lock)
        {
            if (_cells.TryGetValue(key, out var cell))
            {
                int n = cell.Count + 1;
                _cells[key] = (cell.Mean + (altitudeM - cell.Mean) / n, n);
            }
            else
            {
                _cells[key] = (altitudeM, 1);
            }
            if (altitudeM < _minAlt) _minAlt = altitudeM;
            if (altitudeM > _maxAlt) _maxAlt = altitudeM;
            _dirty = true;
        }
    }

    public IReadOnlyList<(double CellE, double CellN, double AltM)> GetCells()
    {
        lock (_lock)
        {
            var result = new List<(double, double, double)>(_cells.Count);
            foreach (var kv in _cells)
                result.Add(((kv.Key.E + 0.5) * CELL_SIZE, (kv.Key.N + 0.5) * CELL_SIZE, kv.Value.Mean));
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cells.Clear();
            _minAlt = double.MaxValue;
            _maxAlt = double.MinValue;
            _dirty = false;
        }
    }

    public void SaveToFile(string fieldDirectory)
    {
        if (string.IsNullOrWhiteSpace(fieldDirectory)) return;
        var inv = CultureInfo.InvariantCulture;
        string json;
        lock (_lock)
        {
            if (!_dirty || _cells.Count == 0) return;
            var sb = new StringBuilder(32 * 1024);
            sb.Append("{\"cell\":").Append(CELL_SIZE.ToString("0.###", inv)).Append(",\"cells\":[");
            bool first = true;
            foreach (var kv in _cells)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('[').Append(kv.Key.E.ToString(inv))
                  .Append(',').Append(kv.Key.N.ToString(inv))
                  .Append(',').Append(kv.Value.Mean.ToString("0.###", inv))
                  .Append(',').Append(kv.Value.Count.ToString(inv)).Append(']');
            }
            sb.Append("]}");
            json = sb.ToString();
            _dirty = false;
        }
        try
        {
            File.WriteAllText(Path.Combine(fieldDirectory, FILE_NAME), json);
        }
        catch (Exception ex)
        {
            lock (_lock) _dirty = true; // failed write: try again next save
            Console.WriteLine($"[Terrain] Error saving {FILE_NAME}: {ex.Message}");
        }
    }

    public void LoadFromFile(string fieldDirectory)
    {
        Clear();
        if (string.IsNullOrWhiteSpace(fieldDirectory)) return;
        var path = Path.Combine(fieldDirectory, FILE_NAME);
        if (!File.Exists(path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("cells", out var cells)
                || cells.ValueKind != JsonValueKind.Array)
                return;
            lock (_lock)
            {
                foreach (var row in cells.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 3) continue;
                    int ce = row[0].GetInt32();
                    int cn = row[1].GetInt32();
                    double alt = row[2].GetDouble();
                    int count = row.GetArrayLength() >= 4 ? Math.Max(1, row[3].GetInt32()) : 1;
                    _cells[(ce, cn)] = (alt, count);
                    if (alt < _minAlt) _minAlt = alt;
                    if (alt > _maxAlt) _maxAlt = alt;
                }
                _dirty = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Terrain] Error loading {FILE_NAME}: {ex.Message}");
            Clear();
        }
    }

    public string BuildElevationJson()
    {
        var inv = CultureInfo.InvariantCulture;
        lock (_lock)
        {
            if (_cells.Count == 0) return "{}";
            var sb = new StringBuilder(32 * 1024);
            sb.Append("{\"cell\":").Append(CELL_SIZE.ToString("0.###", inv))
              .Append(",\"min\":").Append(_minAlt.ToString("0.0", inv))
              .Append(",\"max\":").Append(_maxAlt.ToString("0.0", inv))
              .Append(",\"cells\":[");
            bool first = true;
            foreach (var kv in _cells)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('[').Append(((kv.Key.E + 0.5) * CELL_SIZE).ToString("0.0", inv))
                  .Append(',').Append(((kv.Key.N + 0.5) * CELL_SIZE).ToString("0.0", inv))
                  .Append(',').Append(kv.Value.Mean.ToString("0.0", inv)).Append(']');
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }

    public string? BuildGeoJson(double originLat, double originLon)
    {
        var inv = CultureInfo.InvariantCulture;
        lock (_lock)
        {
            if (_cells.Count == 0) return null;
            var geo = new GeoConversion(originLat, originLon);
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");
            bool first = true;
            foreach (var kv in _cells)
            {
                var (lat, lon) = geo.ToWgs84(new Vec2(
                    (kv.Key.E + 0.5) * CELL_SIZE, (kv.Key.N + 0.5) * CELL_SIZE));
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[")
                  .Append(lon.ToString("0.0000000", inv)).Append(',')
                  .Append(lat.ToString("0.0000000", inv))
                  .Append("]},\"properties\":{\"alt\":")
                  .Append(kv.Value.Mean.ToString("0.##", inv)).Append("}}");
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
