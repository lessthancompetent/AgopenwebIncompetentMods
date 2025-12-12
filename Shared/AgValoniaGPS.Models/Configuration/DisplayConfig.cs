using System;
using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Display and UI configuration.
/// Replaces: display parts of AppSettings, DisplaySettingsService state
/// </summary>
public class DisplayConfig : ReactiveObject
{
    // Map display
    private bool _gridVisible = true;
    public bool GridVisible
    {
        get => _gridVisible;
        set => this.RaiseAndSetIfChanged(ref _gridVisible, value);
    }

    private bool _compassVisible = true;
    public bool CompassVisible
    {
        get => _compassVisible;
        set => this.RaiseAndSetIfChanged(ref _compassVisible, value);
    }

    private bool _speedVisible = true;
    public bool SpeedVisible
    {
        get => _speedVisible;
        set => this.RaiseAndSetIfChanged(ref _speedVisible, value);
    }

    // Camera
    private double _cameraZoom = 100.0;
    public double CameraZoom
    {
        get => _cameraZoom;
        set => this.RaiseAndSetIfChanged(ref _cameraZoom, value);
    }

    private double _cameraPitch = -62.0;
    public double CameraPitch
    {
        get => _cameraPitch;
        set => this.RaiseAndSetIfChanged(ref _cameraPitch, Math.Clamp(value, -90, -10));
    }

    private bool _is2DMode;
    public bool Is2DMode
    {
        get => _is2DMode;
        set => this.RaiseAndSetIfChanged(ref _is2DMode, value);
    }

    private bool _isNorthUp = true;
    public bool IsNorthUp
    {
        get => _isNorthUp;
        set => this.RaiseAndSetIfChanged(ref _isNorthUp, value);
    }

    private bool _isDayMode = true;
    public bool IsDayMode
    {
        get => _isDayMode;
        set => this.RaiseAndSetIfChanged(ref _isDayMode, value);
    }

    // Window (Desktop only, ignored on iOS)
    private double _windowWidth = 1200;
    public double WindowWidth
    {
        get => _windowWidth;
        set => this.RaiseAndSetIfChanged(ref _windowWidth, value);
    }

    private double _windowHeight = 800;
    public double WindowHeight
    {
        get => _windowHeight;
        set => this.RaiseAndSetIfChanged(ref _windowHeight, value);
    }

    private double _windowX = 100;
    public double WindowX
    {
        get => _windowX;
        set => this.RaiseAndSetIfChanged(ref _windowX, value);
    }

    private double _windowY = 100;
    public double WindowY
    {
        get => _windowY;
        set => this.RaiseAndSetIfChanged(ref _windowY, value);
    }

    private bool _windowMaximized;
    public bool WindowMaximized
    {
        get => _windowMaximized;
        set => this.RaiseAndSetIfChanged(ref _windowMaximized, value);
    }

    // Panel positions
    private double _simulatorPanelX = double.NaN;
    public double SimulatorPanelX
    {
        get => _simulatorPanelX;
        set => this.RaiseAndSetIfChanged(ref _simulatorPanelX, value);
    }

    private double _simulatorPanelY = double.NaN;
    public double SimulatorPanelY
    {
        get => _simulatorPanelY;
        set => this.RaiseAndSetIfChanged(ref _simulatorPanelY, value);
    }

    private bool _simulatorPanelVisible;
    public bool SimulatorPanelVisible
    {
        get => _simulatorPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _simulatorPanelVisible, value);
    }
}
