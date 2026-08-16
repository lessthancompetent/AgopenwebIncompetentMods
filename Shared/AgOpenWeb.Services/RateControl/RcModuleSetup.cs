// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Saved copy of what a rate module holds in its own EEPROM: pin assignments,
// module-wide flags and per-sensor valve tuning (PGNs 32700 / 32507 / 32502).
//
// Why keep a copy at all — the module already stores these? Because nothing can
// read them back. There is no "report your config" packet (only PID diagnostics
// and a board label), so if a module is reset, swapped or reflashed, whatever
// was tuned into it is simply gone. Holding the values here makes the TOOL the
// master record of its implement's rate setup: load the tool, push, and the
// module is commissioned again — no Windows machine with the RateController app
// involved, which matters because there no longer is one in the tractor.
//
// Consequence: the first time a module is set up, its existing values have to be
// typed in to match. From then on this file is authoritative.

using System.Collections.Generic;

namespace AgOpenWeb.Services.RateControl;

/// <summary>One sensor (product channel) on a module: its pins and valve tuning.</summary>
public sealed class RcSensorSetup
{
    public int SensorId { get; set; }
    public RcSensorPins Pins { get; set; } = new();
    public RcControlSettings Control { get; set; } = new();
}

/// <summary>One rate module and its sensors.</summary>
public sealed class RcModuleSetup
{
    public int ModuleId { get; set; }

    /// <summary>Which board this module is, so "load defaults" fills the right
    /// pins. Kept per module because a tool can mix boards (an ESP32 on the
    /// sprayer bar, a Nano on the pump). "esp32" (RC15), "teensy" (RC11-2) or
    /// "nano" (RC12-3) — the three the native app ships defaults for.</summary>
    public string Board { get; set; } = "esp32";

    public RcModuleConfig Config { get; set; } = new();
    public List<RcSensorSetup> Sensors { get; set; } = new();

    /// <summary>What each of the 16 relay outputs does. Empty on a setup saved
    /// before relay functions existed — <see cref="EnsureRelays"/> fills it with
    /// the relay-N-drives-section-N default those setups were assuming.</summary>
    public List<RcRelay> Relays { get; set; } = new();

    /// <summary>How the FlowMaster relay drives the valve. Separate from the
    /// module's own <c>Is3WireValve</c>, which is about the product valve.</summary>
    public RcFlowMasterMode FlowMasterMode { get; set; } = RcFlowMasterMode.ThreeWire;

    public RcSensorSetup GetOrAddSensor(int sensorId)
    {
        foreach (var s in Sensors)
            if (s.SensorId == sensorId) return s;
        var added = new RcSensorSetup { SensorId = sensorId };
        Sensors.Add(added);
        return added;
    }

    /// <summary>Make sure all 16 relays exist, without disturbing any already
    /// assigned. Safe to call on every read.</summary>
    public List<RcRelay> EnsureRelays()
    {
        if (Relays.Count == 0) Relays = RcRelayMap.DefaultRelays(ModuleId);
        else
            for (int i = 0; i < RcRelayMap.RelayCount; i++)
                if (!Relays.Exists(r => r.Id == i))
                    Relays.Add(new RcRelay { Id = i, Type = RcRelayType.None, SectionId = -1 });
        Relays.Sort((a, b) => a.Id.CompareTo(b.Id));
        return Relays;
    }
}

/// <summary>Factory pin maps and flags per board, matching the native app's
/// Modules &gt; Boards page. Kept as a pure function of the board name so the
/// table can be checked against the RC sources without standing up a service.</summary>
public static class RcBoardDefaults
{
    private const byte NC = 0xFF;   // firmware's "not connected"

    /// <summary>The board names this understands, in the order the UI lists them.</summary>
    public static readonly string[] Boards = { "esp32", "teensy", "nano" };

    /// <summary>Fill a module with one board's defaults. An unknown board name
    /// falls back to the ESP32 (RC15) — the board this app was built against —
    /// and the module's <see cref="RcModuleSetup.Board"/> is set to match what
    /// was actually applied, never to the unrecognised name.</summary>
    public static void Apply(RcModuleSetup m, string? board)
    {
        byte s0f, s0d, s0p, s1f, s1d, s1p, work, press, onboardRelay;
        bool ads;
        var relays = new byte[16];
        for (int i = 0; i < relays.Length; i++) relays[i] = NC;

        switch ((board ?? "").Trim().ToLowerInvariant())
        {
            case "teensy":  // RC11-2, Teensy 4.1
                board = "teensy";
                s0f = 28; s0d = 37; s0p = 36; s1f = 29; s1d = 14; s1p = 15;
                work = 30; press = 40; onboardRelay = 1; ads = false;
                for (int i = 0; i < 8; i++) relays[i] = new byte[] { 8, 9, 10, 11, 12, 25, 26, 27 }[i];
                break;
            case "nano":    // RC12-3, Arduino Nano. A0-A7 are pins 14-21.
                board = "nano";
                s0f = 3; s0d = 4; s0p = 5; s1f = 2; s1d = 6; s1p = 9;
                work = 15; press = 14; onboardRelay = 4; ads = false;
                relays = new byte[] { 0, 15, 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 };
                break;
            default:        // RC15, ESP32
                board = "esp32";
                s0f = 17; s0d = 32; s0p = 33; s1f = 16; s1d = 25; s1p = 26;
                // The RC15 drives its relays through a PCA9685 by channel, so it
                // has no relay pins at all — and no work or pressure input.
                work = NC; press = NC; onboardRelay = 5; ads = true;
                break;
        }

        var s0 = m.GetOrAddSensor(0);
        s0.Pins = new RcSensorPins { FlowPin = s0f, DirPin = s0d, PwmPin = s0p, BinPin = NC, InvertBinSensor = false };
        if (m.Sensors.Count > 1 || m.Config.SensorCount > 1)
        {
            var s1 = m.GetOrAddSensor(1);
            s1.Pins = new RcSensorPins { FlowPin = s1f, DirPin = s1d, PwmPin = s1p, BinPin = NC, InvertBinSensor = false };
        }

        foreach (var s in m.Sensors)
        {
            // On-wire values matching the firmware defaults: MaxPWM is a percent
            // (the module scales to 255), deadband is percent x10, max integral
            // x10, timed-min-start x100, pulse min Hz x10. PulseSampleSize is a
            // COUNT of pulse times, not a duration — the module caps it at 25.
            s.Control = new RcControlSettings
            {
                MaxPwm = 100, MinPwm = 5, Kp = 45, Ki = 70, Deadband = 15,
                BrakePoint = 35, PidSlowAdjust = 60, SlewRate = 25, MaxIntegral = 250,
                TimedMinStart = 50, TimedAdjust = 80, TimedPause = 400, PidTime = 150,
                PulseMinHz = 10, PulseMaxHz = 4000, PulseSampleSize = 12,
            };
        }

        m.Board = board;
        m.Relays = RcRelayMap.DefaultRelays(m.ModuleId);
        m.FlowMasterMode = RcFlowMasterMode.ThreeWire;
        m.Config.SensorCount = 1;
        m.Config.InvertRelayControl = true;
        m.Config.InvertFlowControl = true;
        m.Config.WorkPinMomentary = false;
        m.Config.Is3WireValve = true;
        m.Config.Ads1115Enabled = ads;
        m.Config.OnboardRelayType = onboardRelay;
        m.Config.RemoteRelayType = 0;
        m.Config.WorkPin = work;
        m.Config.PressurePin = press;
        for (int i = 0; i < m.Config.RelayPins.Length; i++)
            m.Config.RelayPins[i] = i < relays.Length ? relays[i] : NC;
    }
}
