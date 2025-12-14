using System;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Central application state container.
/// Single source of truth for ALL runtime state.
/// Singleton, Observable, Injectable.
/// </summary>
public class ApplicationState : ReactiveObject
{
    private static ApplicationState? _instance;

    /// <summary>
    /// Singleton instance for static access (use DI when possible)
    /// </summary>
    public static ApplicationState Instance => _instance ??= new ApplicationState();

    /// <summary>
    /// Create a new ApplicationState (primarily for DI registration)
    /// </summary>
    public ApplicationState()
    {
        _instance = this;
    }

    // Domain state objects
    public VehicleState Vehicle { get; } = new();
    public GuidanceState Guidance { get; } = new();
    public SectionState Sections { get; } = new();
    public ConnectionState Connections { get; } = new();
    public FieldState Field { get; } = new();
    public YouTurnState YouTurn { get; } = new();
    public BoundaryRecState BoundaryRec { get; } = new();
    public SimulatorState Simulator { get; } = new();
    public UIState UI { get; } = new();

    // Global events
    public event EventHandler? StateReset;

    /// <summary>
    /// Reset all state (e.g., when closing a field)
    /// </summary>
    public void Reset()
    {
        Vehicle.Reset();
        Guidance.Reset();
        Sections.Reset();
        YouTurn.Reset();
        BoundaryRec.Reset();
        Simulator.Reset();
        // Field and Connections typically persist across field changes
        StateReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Full reset including field (e.g., app restart)
    /// </summary>
    public void ResetAll()
    {
        Reset();
        Field.Reset();
        Connections.Reset();
    }
}
