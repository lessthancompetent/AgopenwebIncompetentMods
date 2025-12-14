using AgValoniaGPS.Models.Base;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Current vehicle position, heading, and motion state.
/// Updated by GPS service every frame.
/// </summary>
public class VehicleState : ReactiveObject
{
    // GPS Position (WGS84)
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

    private double _altitude;
    public double Altitude
    {
        get => _altitude;
        set => this.RaiseAndSetIfChanged(ref _altitude, value);
    }

    // Local coordinates (UTM/field plane)
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

    // GPS quality
    private int _fixQuality;
    public int FixQuality
    {
        get => _fixQuality;
        set => this.RaiseAndSetIfChanged(ref _fixQuality, value);
    }

    private int _satelliteCount;
    public int SatelliteCount
    {
        get => _satelliteCount;
        set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    private double _hdop;
    public double Hdop
    {
        get => _hdop;
        set => this.RaiseAndSetIfChanged(ref _hdop, value);
    }

    private double _age;
    public double Age
    {
        get => _age;
        set => this.RaiseAndSetIfChanged(ref _age, value);
    }

    // IMU data
    private double _imuRoll;
    public double ImuRoll
    {
        get => _imuRoll;
        set => this.RaiseAndSetIfChanged(ref _imuRoll, value);
    }

    private double _imuPitch;
    public double ImuPitch
    {
        get => _imuPitch;
        set => this.RaiseAndSetIfChanged(ref _imuPitch, value);
    }

    private double _imuYawRate;
    public double ImuYawRate
    {
        get => _imuYawRate;
        set => this.RaiseAndSetIfChanged(ref _imuYawRate, value);
    }

    // Computed properties
    public string FixQualityText => FixQuality switch
    {
        0 => "No Fix",
        1 => "GPS Fix",
        2 => "DGPS",
        4 => "RTK Fixed",
        5 => "RTK Float",
        _ => $"Unknown ({FixQuality})"
    };

    public bool HasValidFix => FixQuality > 0 && SatelliteCount >= 4;
    public bool HasRtkFix => FixQuality == 4;

    /// <summary>
    /// Vec3 representation for guidance calculations (Easting, Northing, Heading in radians)
    /// </summary>
    public Vec3 PivotPosition => new Vec3(Easting, Northing, Heading * System.Math.PI / 180.0);

    /// <summary>
    /// Vec3 with heading in degrees (as stored)
    /// </summary>
    public Vec3 Position => new Vec3(Easting, Northing, Heading);

    public void Reset()
    {
        Latitude = Longitude = Altitude = 0;
        Easting = Northing = Heading = Speed = 0;
        FixQuality = SatelliteCount = 0;
        Hdop = Age = 0;
        ImuRoll = ImuPitch = ImuYawRate = 0;
    }

    /// <summary>
    /// Update from GPS data
    /// </summary>
    public void UpdateFromGps(Position position, int fixQuality, int satellites, double hdop, double age)
    {
        Latitude = position.Latitude;
        Longitude = position.Longitude;
        Altitude = position.Altitude;
        Easting = position.Easting;
        Northing = position.Northing;
        Heading = position.Heading;
        Speed = position.Speed;
        FixQuality = fixQuality;
        SatelliteCount = satellites;
        Hdop = hdop;
        Age = age;
    }
}
