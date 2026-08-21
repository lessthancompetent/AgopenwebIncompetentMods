// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// How the rate-control switches behave (the native app's Machine > Switches
// page) and Primed Start (Machine > Primed Start). Per TOOL, like everything
// else about an implement's rate setup.

namespace AgOpenWeb.Services.RateControl;

/// <summary>What the master switch controls. Mirrors the native options.</summary>
public enum RcMasterMode
{
    /// <summary>Master controls the master relay and all section relays.</summary>
    ControlAll = 0,
    /// <summary>Master controls only the master relay; sections run independently.</summary>
    MasterRelayOnly = 1,
    /// <summary>Master is always on regardless of the switch.</summary>
    Override = 2,
}

public sealed class RcSwitchboxSettings
{
    public RcMasterMode MasterMode { get; set; } = RcMasterMode.ControlAll;

    /// <summary>0 momentary, 1 maintained. Only matters for a physical box —
    /// kept so the setting is already right when one gets wired — except that
    /// maintained also disables Primed Start, matching the native rule.</summary>
    public int SwitchType { get; set; }

    /// <summary>Show the on-screen switchbox on the main screen.</summary>
    public bool OnScreenEnabled { get; set; } = true;

    /// <summary>The operator's auto-rate switch: gates every channel's auto flag.
    /// Off = manual PWM only, whatever the channels are configured to do.</summary>
    public bool AutoRate { get; set; } = true;

    /// <summary>Master only engages while the implement's work switch is on.</summary>
    public bool WorkSwitchGate { get; set; }

    /// <summary>A physical PGN 32618 box is EXPECTED on this tool — its absence
    /// is alarm-worthy from power-on, not only after it has been seen this
    /// session. Auto-set the first time a box frame arrives on the tool; the
    /// operator can clear it (box removed for good) in Network IO.</summary>
    public bool ExpectPhysical { get; set; }

    /// <summary>Which on-screen switch (0-7) controls each section — the native
    /// "zone" idea: all sections allocated to a switch flip together. Defaults
    /// to switch N for section N (capped at the last switch). -1 = no switch.</summary>
    public int[] SectionSwitch { get; set; } = System.Array.Empty<int>();

    /// <summary>The allocation for a section, defaulting sensibly when the
    /// array is shorter than the section count.</summary>
    public int SwitchFor(int section) =>
        section >= 0 && section < SectionSwitch.Length ? SectionSwitch[section]
        : (section < 8 ? section : 7);
}

public sealed class RcPrimedSettings
{
    /// <summary>How long a primed run lasts, seconds.</summary>
    public double OnTimeS { get; set; } = 10;

    /// <summary>Simulated ground speed used to compute the target while parked.</summary>
    public double SpeedKmh { get; set; } = 8;

    /// <summary>Physical momentary box only: hold-to-prime delay, seconds.</summary>
    public double MasterDelayS { get; set; } = 5;

    /// <summary>After the run, if the machine is actually moving, keep applying.</summary>
    public bool Resume { get; set; }
}
