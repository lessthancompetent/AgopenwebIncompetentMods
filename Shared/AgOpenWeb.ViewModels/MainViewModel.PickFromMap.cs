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

        // GeoCoord is (northing, easting).
        var wgs = lp.ConvertGeoCoordToWgs84(new GeoCoord(northing, easting));

        var fieldsRoot = _settingsService.Settings.FieldsDirectory;
        var nearby = _fieldService.FindFieldsNear(fieldsRoot, wgs.Latitude, wgs.Longitude, maxKm: 0.3);
        if (nearby.Count == 0)
        {
            StatusMessage = "No field near that spot";
            return;
        }

        var pick = nearby[0];
        StatusMessage = $"Opening {pick.Name}…";
        _ = OpenFieldAsync(pick.DirectoryPath, pick.Name);
    }
}
