using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// GPS Simulator state.
/// </summary>
public class SimulatorState : ReactiveObject
{
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    // Position
    private double _latitude;
    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    private double _longitude;
    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    private double _easting;
    public double Easting
    {
        get => _easting;
        set => this.RaiseAndSetIfChanged(ref _easting, value);
    }

    private double _northing;
    public double Northing
    {
        get => _northing;
        set => this.RaiseAndSetIfChanged(ref _northing, value);
    }

    // Motion
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

    // Target speed (slider value)
    private double _targetSpeed = 5.0;
    public double TargetSpeed
    {
        get => _targetSpeed;
        set => this.RaiseAndSetIfChanged(ref _targetSpeed, value);
    }

    // Simulated fix quality
    private int _fixQuality = 4; // RTK Fix by default
    public int FixQuality
    {
        get => _fixQuality;
        set => this.RaiseAndSetIfChanged(ref _fixQuality, value);
    }

    private int _satelliteCount = 12;
    public int SatelliteCount
    {
        get => _satelliteCount;
        set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    public void Reset()
    {
        IsRunning = false;
        Speed = 0;
        SteerAngle = 0;
        // Keep position and enabled state
    }

    public void ResetPosition()
    {
        Latitude = Longitude = 0;
        Easting = Northing = 0;
        Heading = 0;
        Speed = 0;
        SteerAngle = 0;
    }
}
