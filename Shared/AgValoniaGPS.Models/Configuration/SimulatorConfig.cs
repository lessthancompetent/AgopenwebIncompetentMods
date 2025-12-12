using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Simulator configuration - ONE place only.
/// Replaces: simulator fields in both AppSettings and VehicleProfile
/// </summary>
public class SimulatorConfig : ReactiveObject
{
    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set => this.RaiseAndSetIfChanged(ref _enabled, value);
    }

    private double _latitude = 40.7128;
    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    private double _longitude = -74.0060;
    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    private double _heading;
    public double Heading
    {
        get => _heading;
        set => this.RaiseAndSetIfChanged(ref _heading, value);
    }

    private double _speed;
    public double Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }

    private double _steerAngle;
    public double SteerAngle
    {
        get => _steerAngle;
        set => this.RaiseAndSetIfChanged(ref _steerAngle, value);
    }
}
