using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for the Configuration Dialog.
/// Binds directly to ConfigurationStore - no property mapping needed.
/// </summary>
public class ConfigurationViewModel : ReactiveObject
{
    private readonly IConfigurationService _configService;

    #region Dialog Visibility

    private bool _isDialogVisible;
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
    }

    #endregion

    #region Direct Access to Configuration

    /// <summary>
    /// The configuration store - bind directly to sub-configs in XAML
    /// Example: {Binding Config.Vehicle.Wheelbase}
    /// </summary>
    public ConfigurationStore Config => _configService.Store;

    // Convenience accessors for cleaner XAML bindings
    public VehicleConfig Vehicle => Config.Vehicle;
    public ToolConfig Tool => Config.Tool;
    public GuidanceConfig Guidance => Config.Guidance;
    public DisplayConfig Display => Config.Display;
    public SimulatorConfig Simulator => Config.Simulator;

    #endregion

    #region Profile Management

    public ObservableCollection<string> AvailableProfiles { get; } = new();

    private string? _selectedProfileName;
    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value != null && value != Config.ActiveProfileName)
            {
                _configService.LoadProfile(value);
            }
        }
    }

    /// <summary>
    /// Whether there are unsaved changes (delegates to ConfigurationStore)
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => Config.HasUnsavedChanges;
        set => Config.HasUnsavedChanges = value;
    }

    #endregion

    #region Commands

    public ICommand LoadProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SetToolTypeCommand { get; }
    public ICommand SetVehicleTypeCommand { get; }

    #endregion

    #region Events

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ProfileSaved;

    #endregion

    public ConfigurationViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // Initialize commands
        LoadProfileCommand = new RelayCommand<string>(LoadProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        NewProfileCommand = new RelayCommand<string>(CreateNewProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyCommand = new RelayCommand(ApplyChanges);
        CancelCommand = new RelayCommand(Cancel);
        SetToolTypeCommand = new RelayCommand<string>(SetToolType);
        SetVehicleTypeCommand = new RelayCommand<string>(SetVehicleType);

        // Subscribe to config changes for HasUnsavedChanges notification
        Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfigurationStore.HasUnsavedChanges))
            {
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));
            }
        };

        // Load available profiles
        RefreshProfileList();

        // Set selected profile name to current
        _selectedProfileName = Config.ActiveProfileName;
    }

    private void RefreshProfileList()
    {
        AvailableProfiles.Clear();
        foreach (var profileName in _configService.GetAvailableProfiles())
        {
            AvailableProfiles.Add(profileName);
        }
    }

    private void LoadProfile(string? profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return;
        _configService.LoadProfile(profileName);
        _selectedProfileName = profileName;
        this.RaisePropertyChanged(nameof(SelectedProfileName));
    }

    private void SaveProfile()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        ProfileSaved?.Invoke(this, Config.ActiveProfileName);
    }

    private void CreateNewProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;

        _configService.CreateProfile(profileName);
        RefreshProfileList();
        _selectedProfileName = profileName;
        this.RaisePropertyChanged(nameof(SelectedProfileName));
    }

    private void DeleteProfile()
    {
        if (string.IsNullOrEmpty(SelectedProfileName)) return;
        if (SelectedProfileName == Config.ActiveProfileName) return; // Can't delete active

        _configService.DeleteProfile(SelectedProfileName);
        RefreshProfileList();
    }

    private void ApplyChanges()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        _configService.ReloadCurrentProfile();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetToolType(string? toolType)
    {
        if (string.IsNullOrEmpty(toolType)) return;
        Tool.SetToolType(toolType);
        Config.MarkChanged();
    }

    private void SetVehicleType(string? vehicleType)
    {
        if (string.IsNullOrEmpty(vehicleType)) return;

        Vehicle.Type = vehicleType.ToLowerInvariant() switch
        {
            "tractor" => VehicleType.Tractor,
            "harvester" => VehicleType.Harvester,
            "fourwd" or "4wd" or "articulated" => VehicleType.FourWD,
            _ => VehicleType.Tractor
        };
        Config.MarkChanged();
    }
}
