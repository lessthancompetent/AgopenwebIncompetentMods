using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Connection status for all external systems.
/// Updated by communication services.
/// </summary>
public class ConnectionState : ReactiveObject
{
    // GPS
    private bool _isGpsConnected;
    public bool IsGpsConnected
    {
        get => _isGpsConnected;
        set => this.RaiseAndSetIfChanged(ref _isGpsConnected, value);
    }

    private bool _isGpsDataOk;
    public bool IsGpsDataOk
    {
        get => _isGpsDataOk;
        set => this.RaiseAndSetIfChanged(ref _isGpsDataOk, value);
    }

    // NTRIP
    private bool _isNtripConnected;
    public bool IsNtripConnected
    {
        get => _isNtripConnected;
        set => this.RaiseAndSetIfChanged(ref _isNtripConnected, value);
    }

    private string _ntripStatus = "Not Connected";
    public string NtripStatus
    {
        get => _ntripStatus;
        set => this.RaiseAndSetIfChanged(ref _ntripStatus, value);
    }

    private ulong _ntripBytesReceived;
    public ulong NtripBytesReceived
    {
        get => _ntripBytesReceived;
        set => this.RaiseAndSetIfChanged(ref _ntripBytesReceived, value);
    }

    // AutoSteer module
    private bool _isAutoSteerConnected;
    public bool IsAutoSteerConnected
    {
        get => _isAutoSteerConnected;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerConnected, value);
    }

    private bool _isAutoSteerDataOk;
    public bool IsAutoSteerDataOk
    {
        get => _isAutoSteerDataOk;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerDataOk, value);
    }

    private bool _isAutoSteerEngaged;
    public bool IsAutoSteerEngaged
    {
        get => _isAutoSteerEngaged;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerEngaged, value);
    }

    // Machine module
    private bool _isMachineConnected;
    public bool IsMachineConnected
    {
        get => _isMachineConnected;
        set => this.RaiseAndSetIfChanged(ref _isMachineConnected, value);
    }

    private bool _isMachineDataOk;
    public bool IsMachineDataOk
    {
        get => _isMachineDataOk;
        set => this.RaiseAndSetIfChanged(ref _isMachineDataOk, value);
    }

    // IMU
    private bool _isImuConnected;
    public bool IsImuConnected
    {
        get => _isImuConnected;
        set => this.RaiseAndSetIfChanged(ref _isImuConnected, value);
    }

    private bool _isImuDataOk;
    public bool IsImuDataOk
    {
        get => _isImuDataOk;
        set => this.RaiseAndSetIfChanged(ref _isImuDataOk, value);
    }

    // Overall status
    public bool IsFullyConnected =>
        IsGpsConnected && IsAutoSteerConnected && IsMachineConnected;

    public string OverallStatus
    {
        get
        {
            if (!IsGpsConnected) return "No GPS";
            if (!IsAutoSteerConnected) return "No AutoSteer";
            if (!IsMachineConnected) return "No Machine";
            if (!IsNtripConnected) return "No RTK";
            return "Connected";
        }
    }

    public void Reset()
    {
        // Connection state typically persists, but provide reset for full app restart
        IsGpsConnected = IsGpsDataOk = false;
        IsNtripConnected = false;
        NtripStatus = "Not Connected";
        NtripBytesReceived = 0;
        IsAutoSteerConnected = IsAutoSteerDataOk = IsAutoSteerEngaged = false;
        IsMachineConnected = IsMachineDataOk = false;
        IsImuConnected = IsImuDataOk = false;
    }
}
