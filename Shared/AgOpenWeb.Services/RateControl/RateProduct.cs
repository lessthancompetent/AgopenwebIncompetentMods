// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Text.Json.Serialization;

namespace AgOpenWeb.Services.RateControl;

/// <summary>
/// One controlled product (tank/bin), ported from AOG_RC's clsProduct.
/// Configuration persists to products.json; live values come from the module's
/// PGN 32400 stream and this app's speed/section state.
/// </summary>
public sealed class RateProduct
{
    // ---- persisted configuration ----
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public int ModuleId { get; set; }
    public int SensorId { get; set; }
    /// <summary>Target application rate (units per coverage unit, e.g. L/ha).</summary>
    public double TargetRate { get; set; } = 100;
    /// <summary>0 = acres, 1 = hectares, 2 = per minute, 3 = per hour (AOG_RC order).</summary>
    public int CoverageUnits { get; set; } = 1;
    /// <summary>Flow meter calibration — pulses per unit.</summary>
    public double MeterCal { get; set; } = 180;
    public RcControlType ControlType { get; set; } = RcControlType.Motor;
    /// <summary>true = constant UPM regardless of sections; false = section-scaled.</summary>
    public bool ConstantUpm { get; set; }
    /// <summary>Floor for the commanded UPM while applying (0 = none).</summary>
    public double MinUpm { get; set; }
    /// <summary>Units the tank/bin holds when full (for the remaining gauge).</summary>
    public double TankSize { get; set; } = 1000;
    public double TankRemaining { get; set; } = 1000;
    /// <summary>Auto (PID chases target) vs manual PWM.</summary>
    public bool AutoOn { get; set; } = true;
    public int ManualPwm { get; set; }
    /// <summary>Session totals (persisted so they survive restarts, like AOG_RC).</summary>
    public double QuantityApplied { get; set; }
    public double AreaApplied { get; set; }

    // ---- live, not persisted ----
    [JsonIgnore] public double MeasuredUpm { get; set; }
    [JsonIgnore] public double ModuleAccumulated { get; set; }
    [JsonIgnore] public int ModulePwm { get; set; }
    [JsonIgnore] public bool BinEmpty { get; set; }
    [JsonIgnore] public double Hz { get; set; }
    [JsonIgnore] public DateTime LastFrameUtc { get; set; }
    [JsonIgnore] public bool AccumSeeded { get; set; }
    [JsonIgnore] public double AccumLast { get; set; }
    [JsonIgnore] public bool ResetQuantityPending { get; set; }

    [JsonIgnore]
    public bool ModuleConnected => (DateTime.UtcNow - LastFrameUtc).TotalSeconds < 4;

    /// <summary>Units-per-minute target for the module PID, from AOG_RC's
    /// TargetUPM(): rate × worked-area-per-minute (or plain rate for the
    /// time-based units). <paramref name="activeHaPerMin"/> is width-on ×
    /// speed / 600; <paramref name="totalHaPerMin"/> uses full tool width.</summary>
    public double TargetUpm(double activeHaPerMin, double totalHaPerMin)
    {
        if (!Enabled) return 0;
        double haPerMin = ConstantUpm
            ? (activeHaPerMin < 0.0001 ? 0 : totalHaPerMin) // all-off still zeroes
            : activeHaPerMin;
        return CoverageUnits switch
        {
            0 => TargetRate * haPerMin * 2.47105, // acres
            1 => TargetRate * haPerMin,           // hectares
            2 => TargetRate,                      // units per minute
            _ => TargetRate / 60.0,               // units per hour
        };
    }

    /// <summary>Fold a module sensor frame in: live values + measured-quantity
    /// accumulation by delta of the module's lifetime counter (reset/rollover
    /// guarded, mirroring AOG_RC's UpdateUnitsApplied).</summary>
    public void ApplySensorFrame(in RcSensorFrame f)
    {
        MeasuredUpm = f.Upm;
        ModulePwm = f.Pwm;
        BinEmpty = f.BinEmpty;
        Hz = f.Hz;
        ModuleAccumulated = f.AccumulatedQuantity;
        LastFrameUtc = DateTime.UtcNow;

        if (AccumSeeded)
        {
            double diff = f.AccumulatedQuantity - AccumLast;
            if (diff < 0 || diff > 1000) diff = 0; // module reset / rollover
            QuantityApplied += diff;
            TankRemaining = Math.Max(0, TankRemaining - diff);
        }
        AccumLast = f.AccumulatedQuantity;
        AccumSeeded = true;
    }
}
