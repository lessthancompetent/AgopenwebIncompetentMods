// AgOpenWeb — pick-from-map (ported from AgValoniaGPS-RoutePlanner fork)
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgOpenWeb.Models;

namespace AgOpenWeb.ViewModels;

public partial class MainViewModel
{
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
