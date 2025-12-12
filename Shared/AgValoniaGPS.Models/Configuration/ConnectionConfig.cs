using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Network and communication configuration.
/// Replaces: NTRIP and AgShare parts of AppSettings
/// </summary>
public class ConnectionConfig : ReactiveObject
{
    // NTRIP
    private string _ntripCasterHost = string.Empty;
    public string NtripCasterHost
    {
        get => _ntripCasterHost;
        set => this.RaiseAndSetIfChanged(ref _ntripCasterHost, value);
    }

    private int _ntripCasterPort = 2101;
    public int NtripCasterPort
    {
        get => _ntripCasterPort;
        set => this.RaiseAndSetIfChanged(ref _ntripCasterPort, value);
    }

    private string _ntripMountPoint = string.Empty;
    public string NtripMountPoint
    {
        get => _ntripMountPoint;
        set => this.RaiseAndSetIfChanged(ref _ntripMountPoint, value);
    }

    private string _ntripUsername = string.Empty;
    public string NtripUsername
    {
        get => _ntripUsername;
        set => this.RaiseAndSetIfChanged(ref _ntripUsername, value);
    }

    private string _ntripPassword = string.Empty;
    public string NtripPassword
    {
        get => _ntripPassword;
        set => this.RaiseAndSetIfChanged(ref _ntripPassword, value);
    }

    private bool _ntripAutoConnect;
    public bool NtripAutoConnect
    {
        get => _ntripAutoConnect;
        set => this.RaiseAndSetIfChanged(ref _ntripAutoConnect, value);
    }

    // AgShare
    private string _agShareServer = "https://agshare.agopengps.com";
    public string AgShareServer
    {
        get => _agShareServer;
        set => this.RaiseAndSetIfChanged(ref _agShareServer, value);
    }

    private string _agShareApiKey = string.Empty;
    public string AgShareApiKey
    {
        get => _agShareApiKey;
        set => this.RaiseAndSetIfChanged(ref _agShareApiKey, value);
    }

    private bool _agShareEnabled;
    public bool AgShareEnabled
    {
        get => _agShareEnabled;
        set => this.RaiseAndSetIfChanged(ref _agShareEnabled, value);
    }

    // GPS
    private int _gpsUpdateRate = 10;
    public int GpsUpdateRate
    {
        get => _gpsUpdateRate;
        set => this.RaiseAndSetIfChanged(ref _gpsUpdateRate, value);
    }

    private bool _useRtk = true;
    public bool UseRtk
    {
        get => _useRtk;
        set => this.RaiseAndSetIfChanged(ref _useRtk, value);
    }
}
