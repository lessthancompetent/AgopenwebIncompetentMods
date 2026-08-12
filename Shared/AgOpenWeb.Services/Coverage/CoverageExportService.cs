// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Coverage export: turns the job's ACTUALLY-DRIVEN coverage (the painted display
// cells) into a compact WGS84 GeoJSON application record — the union of the swept
// area as MultiPolygon(s), tagged with the job's product/rate metadata. One
// coverage.geojson per job folder; files are the archival record (a server-side
// database is a rebuildable index over them). Geometry pipeline: merge each grid
// row's cells into horizontal rectangles, Clipper-union them (PolyTree so holes
// nest correctly), simplify, convert local metres → lat/lon.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AgOpenWeb.Models;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.Coverage;
using Clipper2Lib;

namespace AgOpenWeb.Services.Coverage;

public static class CoverageExportService
{
    private const double S = 100.0;          // int scale: cm precision
    private const double SimplifyM = 0.4;    // vertex simplification (metres)
    private const double MinRingAreaM2 = 4.0; // drop slivers under this

    /// <summary>
    /// Build the GeoJSON application record, or null when there is no coverage.
    /// <paramref name="cells"/> are painted display cells indexed from the grid
    /// origin (<paramref name="minE"/>, <paramref name="minN"/>) at
    /// <paramref name="cellSize"/> metres.
    /// </summary>
    public static string? BuildGeoJson(
        IReadOnlyList<(int X, int Y, CoverageColor Color, int Alpha)> cells,
        double cellSize, double minE, double minN,
        double originLat, double originLon,
        string fieldName, string taskName, string product, double rate, string rateUnit,
        string workType, DateTime startedAt, DateTime? endedAt, double toolWidthM,
        double appliedAmount = 0, string appliedUnit = "")
    {
        if (cells == null || cells.Count == 0 || cellSize <= 0.001) return null;

        // 1. Merge each row's cells into horizontal run rectangles (thousands of
        //    rects instead of hundreds of thousands of squares before the union).
        var rows = new Dictionary<int, List<int>>();
        foreach (var c in cells)
        {
            if (!rows.TryGetValue(c.Y, out var list)) rows[c.Y] = list = new List<int>();
            list.Add(c.X);
        }
        var rects = new Paths64();
        foreach (var kv in rows)
        {
            var xs = kv.Value;
            xs.Sort();
            int runStart = xs[0], prev = xs[0];
            for (int i = 1; i <= xs.Count; i++)
            {
                if (i < xs.Count && (xs[i] == prev || xs[i] == prev + 1)) { prev = xs[i]; continue; }
                long x0 = (long)Math.Round((minE + runStart * cellSize) * S);
                long x1 = (long)Math.Round((minE + (prev + 1) * cellSize) * S);
                long y0 = (long)Math.Round((minN + kv.Key * cellSize) * S);
                long y1 = (long)Math.Round((minN + (kv.Key + 1) * cellSize) * S);
                rects.Add(new Path64 { new Point64(x0, y0), new Point64(x1, y0), new Point64(x1, y1), new Point64(x0, y1) });
                if (i < xs.Count) { runStart = xs[i]; prev = xs[i]; }
            }
        }
        if (rects.Count == 0) return null;

        // 2. Union with a PolyTree so outer rings and their holes nest properly.
        var tree = new PolyTree64();
        var clipper = new Clipper64();
        clipper.AddSubject(rects);
        clipper.Execute(ClipType.Union, FillRule.NonZero, tree);

        // 3. Walk the tree: each top-level (and each even-depth) node is a polygon
        //    outer ring; its direct children are holes.
        var geo = new GeoConversion(originLat, originLon);
        var inv = CultureInfo.InvariantCulture;
        var polys = new List<List<Path64>>();     // [outer, hole, hole...] per polygon
        void Walk(PolyPath64 node, bool isHole)
        {
            foreach (var child in node)
            {
                var p = (PolyPath64)child;
                var ring = Clipper.SimplifyPath(p.Polygon!, SimplifyM * S);
                bool keep = ring.Count >= 3 && Math.Abs(Clipper.Area(ring)) >= MinRingAreaM2 * S * S;
                if (!isHole)
                {
                    if (keep) { polys.Add(new List<Path64> { ring }); Walk(p, true); }
                    // dropped sliver outer: don't recurse (its holes are meaningless)
                }
                else
                {
                    if (keep && polys.Count > 0) polys[^1].Add(ring);
                    Walk(p, false);   // a hole's children are new outer polygons
                }
            }
        }
        Walk(tree, false);
        if (polys.Count == 0) return null;

        // 4. Worked area from the painted cells themselves (the honest figure).
        double workedHa = cells.Count * cellSize * cellSize / 10000.0;

        // 5. Emit GeoJSON. Coordinates [lon, lat], 7 decimals (~1 cm).
        string Ring(Path64 ring)
        {
            var sb2 = new StringBuilder("[");
            for (int i = 0; i <= ring.Count; i++)   // close the ring
            {
                var pt = ring[i % ring.Count];
                var (lat, lon) = geo.ToWgs84(new Vec2(pt.X / S, pt.Y / S));
                if (i > 0) sb2.Append(',');
                sb2.Append('[').Append(lon.ToString("0.0000000", inv)).Append(',')
                   .Append(lat.ToString("0.0000000", inv)).Append(']');
            }
            return sb2.Append(']').ToString();
        }
        string J(string s2) => s2.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"Feature\",\"geometry\":{\"type\":\"MultiPolygon\",\"coordinates\":[");
        for (int pi = 0; pi < polys.Count; pi++)
        {
            if (pi > 0) sb.Append(',');
            sb.Append('[');
            for (int ri = 0; ri < polys[pi].Count; ri++)
            {
                if (ri > 0) sb.Append(',');
                sb.Append(Ring(polys[pi][ri]));
            }
            sb.Append(']');
        }
        sb.Append("]},\"properties\":{")
          .Append("\"schema\":1")
          .Append(",\"field\":\"").Append(J(fieldName)).Append('"')
          .Append(",\"job\":\"").Append(J(taskName)).Append('"')
          .Append(",\"product\":\"").Append(J(product)).Append('"')
          .Append(",\"rate\":").Append(rate.ToString("0.###", inv))
          .Append(",\"rateUnit\":\"").Append(J(rateUnit)).Append('"')
          .Append(",\"workType\":\"").Append(J(workType)).Append('"')
          .Append(",\"workedHa\":").Append(workedHa.ToString("0.####", inv))
          .Append(",\"toolWidthM\":").Append(toolWidthM.ToString("0.##", inv));
        // Applied stats: measured total (loader scale / tank total / drill counter)
        // beats an estimate; otherwise target rate × area when a rate is known.
        // actualRate is per-ha over the actually-painted area either way.
        double applied = appliedAmount > 0 ? appliedAmount
                       : (rate > 0 && workedHa > 0 ? rate * workedHa : 0);
        if (applied > 0)
        {
            string unit = appliedAmount > 0 && !string.IsNullOrWhiteSpace(appliedUnit)
                ? appliedUnit
                : rateUnit.Split('/')[0];  // "kg/ha" → "kg"
            sb.Append(",\"appliedAmount\":").Append(applied.ToString("0.###", inv))
              .Append(",\"appliedUnit\":\"").Append(J(unit)).Append('"')
              .Append(",\"appliedMeasured\":").Append(appliedAmount > 0 ? "true" : "false");
            if (workedHa > 0)
                sb.Append(",\"actualRate\":").Append((applied / workedHa).ToString("0.###", inv));
        }
        sb
          .Append(",\"startedAt\":\"").Append(startedAt.ToString("o", inv)).Append('"');
        if (endedAt.HasValue)
            sb.Append(",\"endedAt\":\"").Append(endedAt.Value.ToString("o", inv)).Append('"');
        sb.Append(",\"exportedAt\":\"").Append(DateTime.Now.ToString("o", inv)).Append('"')
          .Append("}}");
        return sb.ToString();
    }
}
