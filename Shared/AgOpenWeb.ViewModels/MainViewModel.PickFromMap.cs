// AgOpenWeb — pick-from-map (ported from AgValoniaGPS-RoutePlanner fork)
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AgOpenWeb.Models;

namespace AgOpenWeb.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// JSON of every georeferenced field's outer boundary, projected into the
    /// CURRENT map plane (metres), for the web to draw as pick-from-map outlines:
    /// <c>[{"name":"…","ring":[[e,n],…]},…]</c>. Lets the operator see + tap fields
    /// without the satellite background. Served at /api/nearbyfields.
    /// </summary>
    public string GetNearbyFieldOutlinesJson()
    {
        var lp = State.Field.LocalPlane;
        var root = _settingsService.Settings.FieldsDirectory;
        if (lp == null || string.IsNullOrEmpty(root) || !Directory.Exists(root)) return "[]";

        var sb = new StringBuilder("[");
        bool firstField = true;
        foreach (var dir in Directory.GetDirectories(root))
        {
            Field field;
            try { field = _fieldService.LoadField(dir); }
            catch { continue; }
            if (field?.Boundary?.OuterBoundary is not { IsValid: true } outer) continue;
            if (field.Origin.Latitude == 0 && field.Origin.Longitude == 0) continue;

            var fieldPlane = new LocalPlane(
                new Wgs84(field.Origin.Latitude, field.Origin.Longitude), new SharedFieldProperties());

            if (!firstField) sb.Append(',');
            firstField = false;
            sb.Append("{\"name\":")
              .Append(JsonSerializer.Serialize(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar))))
              .Append(",\"ring\":[");
            bool firstPt = true;
            foreach (var p in outer.Points)
            {
                // field-local (E,N) -> wgs -> current map plane (E,N).
                var w = fieldPlane.ConvertGeoCoordToWgs84(new GeoCoord(p.Northing, p.Easting));
                var loc = lp.ConvertWgs84ToGeoCoord(w);
                if (!firstPt) sb.Append(',');
                firstPt = false;
                sb.Append('[')
                  .Append(loc.Easting.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                  .Append(loc.Northing.ToString("0.###", CultureInfo.InvariantCulture)).Append(']');
            }
            sb.Append("]}");
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Open the field at a tapped map point. The web arms a map tap (startMapTap),
    /// which unprojects to local-plane metres and sends "field.tapOpen|e,n"; here we
    /// convert that back to lat/lon via the active LocalPlane and reuse the existing
    /// FindFieldsNear + OpenFieldAsync path (same as the "Drive In" shortcut, but
    /// keyed off the tapped point instead of the GPS fix).
    /// </summary>
    public void TryOpenFieldAtTap(double easting, double northing)
    {
        var lp = State.Field.LocalPlane;
        if (lp == null)
        {
            StatusMessage = "No map origin yet — need a GPS fix first";
            return;
        }

        // Tapped point (current map plane, metres) -> lat/lon. GeoCoord is (N, E).
        var wgs = lp.ConvertGeoCoordToWgs84(new GeoCoord(northing, easting));

        var fieldsRoot = _settingsService.Settings.FieldsDirectory;
        var nearby = _fieldService.FindFieldsNear(fieldsRoot, wgs.Latitude, wgs.Longitude, maxKm: 0.5);
        if (nearby.Count == 0)
        {
            StatusMessage = "No field there";
            return;
        }

        // Open the field whose boundary actually CONTAINS the tap, not merely the
        // nearest origin (a field origin often isn't its centre). Convert the tapped
        // lat/lon into each candidate's own plane and use its boundary containment.
        foreach (var nf in nearby)
        {
            Field field;
            try { field = _fieldService.LoadField(nf.DirectoryPath); }
            catch { continue; }
            if (field?.Boundary is not { IsValid: true }) continue;
            if (field.Origin.Latitude == 0 && field.Origin.Longitude == 0) continue;

            var fieldPlane = new LocalPlane(
                new Wgs84(field.Origin.Latitude, field.Origin.Longitude), new SharedFieldProperties());
            var loc = fieldPlane.ConvertWgs84ToGeoCoord(wgs); // field-plane metres
            if (field.Boundary.IsPointInside(loc.Easting, loc.Northing))
            {
                StatusMessage = $"Opening {nf.Name}…";
                _ = OpenFieldAsync(nf.DirectoryPath, nf.Name);
                return;
            }
        }

        StatusMessage = "Tap inside a field's boundary to open it";
    }
}
