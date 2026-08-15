// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// RC module-plane codecs, pinned against REAL frames captured from the user's
// RC15 module (2026-08-14 bench) — not synthetic examples. The RC plane frames
// carry a little-endian 16-bit id in bytes 0-1 and an additive CRC summed from
// byte 0 (both unlike the AOG plane).

using System;
using AgOpenWeb.Services.RateControl;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class RcPgnTests
{
    private static byte[] Hex(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var b = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++) b[i] = Convert.ToByte(parts[i], 16);
        return b;
    }

    // Live capture: RC15 module 0/sensor 0, idle (no flow), accumulated counter,
    // sensor-connected bit set.
    private static readonly byte[] Live32400 = Hex("90 7E 00 00 00 00 D0 73 2F 00 00 01 00 00 81");

    // Live capture: module 0 status — InoType 4, InoID 0x621B, ethernet + good pins.
    private static readonly byte[] Live32401 = Hex("91 7E 00 00 00 00 00 00 00 00 04 1B 62 30 C0");

    [Test]
    public void Sensor32400_ParsesLiveRc15Frame()
    {
        Assert.That(RcPgn.TryParseSensor(Live32400, out var f), Is.True);
        Assert.That(f.ModuleId, Is.Zero);
        Assert.That(f.SensorId, Is.Zero);
        Assert.That(f.Upm, Is.Zero, "idle module, no flow");
        Assert.That(f.AccumulatedQuantity, Is.EqualTo(0x2F73D0 / 10.0).Within(0.01),
            "lifetime counter: 3 bytes LE / 10");
        Assert.That(f.Pwm, Is.Zero);
        Assert.That(f.SensorReceiving, Is.True);
        Assert.That(f.BinEmpty, Is.False);
    }

    [Test]
    public void ModuleStatus32401_ParsesLiveRc15Frame()
    {
        Assert.That(RcPgn.TryParseModuleStatus(Live32401, out var f), Is.True);
        Assert.That(f.ModuleId, Is.Zero);
        Assert.That(f.InoType, Is.EqualTo(4));
        Assert.That(f.InoId, Is.EqualTo(0x621B));
        Assert.That(f.EthernetConnected, Is.True);
        Assert.That(f.GoodPinConfig, Is.True);
        Assert.That(f.WorkSwitch, Is.False);
    }

    [Test]
    public void Crc_MatchesLiveFrames_AndRejectsCorruption()
    {
        Assert.That(RcPgn.GoodCrc(Live32400), Is.True);
        Assert.That(RcPgn.GoodCrc(Live32401), Is.True);
        var bad = (byte[])Live32400.Clone();
        bad[6] ^= 0xFF;
        Assert.That(RcPgn.TryParseSensor(bad, out _), Is.False, "corrupted frame must fail CRC");
    }

    [Test]
    public void RateSettings32500_BuildsStockLayout()
    {
        // Motor control, auto on, master on, target 42.5 u/min, cal 180.5.
        var d = RcPgn.BuildRateSettings(
            moduleId: 2, sensorId: 1, targetUpm: 42.5, meterCal: 180.5,
            RcControlType.Motor, masterOn: true, autoOn: true, resetQuantity: false,
            manualPwm: -120, productEnabled: true);

        Assert.That(d.Length, Is.EqualTo(14));
        Assert.That(d[0], Is.EqualTo(0xF4));
        Assert.That(d[1], Is.EqualTo(0x7E));
        Assert.That(d[2], Is.EqualTo((2 << 4) | 1), "mod/sen packed nibbles");
        Assert.That(d[5] << 16 | d[4] << 8 | d[3], Is.EqualTo(42500), "rate x1000 LE24");
        Assert.That(d[8] << 16 | d[7] << 8 | d[6], Is.EqualTo(180500), "cal x1000 LE24");
        Assert.That(d[9], Is.EqualTo(4 | 16 | 64), "Motor bits + MasterOn + AutoOn");
        Assert.That((short)(d[11] << 8 | d[10]), Is.EqualTo(-120), "manual PWM int16");
        Assert.That(RcPgn.GoodCrc(d), Is.True, "additive CRC from byte 0");
    }

    [Test]
    public void RateSettings32500_DisabledProduct_ZeroesRateAndPwm()
    {
        var d = RcPgn.BuildRateSettings(0, 0, 42.5, 180, RcControlType.Valve,
            masterOn: true, autoOn: false, resetQuantity: true, manualPwm: 100,
            productEnabled: false);
        Assert.That(d[3] | d[4] | d[5], Is.Zero, "no rate for disabled product");
        Assert.That(d[10] | d[11], Is.Zero, "no PWM for disabled product");
        Assert.That(d[9] & 1, Is.EqualTo(1), "reset-quantity bit still honoured");
    }

    [Test]
    public void RelaySettings32501_BuildsStockLayout()
    {
        // Sections 1,2,3,10 on → relayLo 0b0000_0111, relayHi 0b0000_0010.
        var d = RcPgn.BuildRelaySettings(moduleId: 1, relayLo: 0b0000_0111, relayHi: 0b0000_0010);

        Assert.That(d.Length, Is.EqualTo(11));
        Assert.That(d[0], Is.EqualTo(0xF5));
        Assert.That(d[1], Is.EqualTo(0x7E));
        Assert.That(d[2], Is.EqualTo(1 << 4), "module id in high nibble");
        Assert.That(d[3], Is.EqualTo(0b0000_0111));
        Assert.That(d[4], Is.EqualTo(0b0000_0010));
        Assert.That(d[9], Is.EqualTo(255), "no flow master valve");
        Assert.That(RcPgn.GoodCrc(d), Is.True);
    }

    [Test]
    public void TargetUpm_MatchesAogRcMath()
    {
        var p = new RateProduct { Enabled = true, TargetRate = 100, CoverageUnits = 1 }; // 100 u/ha
        // 15 m active width at 10 km/h -> 0.25 ha/min -> 25 u/min.
        double haMin = 15.0 * 10.0 / 600.0;
        Assert.That(p.TargetUpm(haMin, haMin), Is.EqualTo(25.0).Within(1e-9));

        // ConstantUPM: full width even with half the sections off.
        p.ConstantUpm = true;
        Assert.That(p.TargetUpm(haMin / 2, haMin), Is.EqualTo(25.0).Within(1e-9));
        // ...but all-off still zeroes.
        Assert.That(p.TargetUpm(0, haMin), Is.Zero);

        // Acres mode multiplies by 2.47105; per-minute mode is the rate itself.
        p.ConstantUpm = false;
        p.CoverageUnits = 0;
        Assert.That(p.TargetUpm(haMin, haMin), Is.EqualTo(25.0 * 2.47105).Within(1e-6));
        p.CoverageUnits = 2;
        Assert.That(p.TargetUpm(haMin, haMin), Is.EqualTo(100));
    }

    [Test]
    public void ActualRate_InvertsTargetUpm_ForTheReadout()
    {
        // The on-map readout puts actual beside target, so the two must be in the
        // same units: feeding the target flow back in has to read as the target.
        var p = new RateProduct { Enabled = true, TargetRate = 100, CoverageUnits = 1 };
        double haMin = 15.0 * 10.0 / 600.0;            // 15 m at 10 km/h
        p.MeasuredUpm = p.TargetUpm(haMin, haMin);
        Assert.That(p.ActualRate(haMin, haMin), Is.EqualTo(100).Within(1e-9));

        // Acres and per-minute modes round-trip too.
        p.CoverageUnits = 0;
        p.MeasuredUpm = p.TargetUpm(haMin, haMin);
        Assert.That(p.ActualRate(haMin, haMin), Is.EqualTo(100).Within(1e-6));
        p.CoverageUnits = 2;
        p.MeasuredUpm = p.TargetUpm(haMin, haMin);
        Assert.That(p.ActualRate(haMin, haMin), Is.EqualTo(100).Within(1e-9));
    }

    [Test]
    public void ActualRate_IsZeroWhenNotCovering()
    {
        // Stopped, or every section shut: units-per-hectare is meaningless and a
        // divide by ~0 would show the operator a wild number.
        var p = new RateProduct { Enabled = true, TargetRate = 100, CoverageUnits = 1, MeasuredUpm = 25 };
        Assert.That(p.ActualRate(0, 0), Is.Zero);
        p.ConstantUpm = true;
        Assert.That(p.ActualRate(0, 0.25), Is.Zero, "all-off zeroes even in constant-UPM");
        p.Enabled = false;
        Assert.That(p.ActualRate(0.25, 0.25), Is.Zero, "disabled channel shows nothing");
    }

    [Test]
    public void ApplySensorFrame_AccumulatesByDelta_GuardsReset()
    {
        var p = new RateProduct { Enabled = true };
        var f1 = new RcSensorFrame { AccumulatedQuantity = 1000.0, Upm = 30 };
        p.ApplySensorFrame(f1);
        Assert.That(p.QuantityApplied, Is.Zero, "first frame seeds, no delta");

        p.ApplySensorFrame(new RcSensorFrame { AccumulatedQuantity = 1004.5 });
        Assert.That(p.QuantityApplied, Is.EqualTo(4.5).Within(1e-9));

        // Module reset: counter drops — delta ignored, not negative.
        p.ApplySensorFrame(new RcSensorFrame { AccumulatedQuantity = 2.0 });
        Assert.That(p.QuantityApplied, Is.EqualTo(4.5).Within(1e-9));

        // Resumes from the new baseline.
        p.ApplySensorFrame(new RcSensorFrame { AccumulatedQuantity = 3.0 });
        Assert.That(p.QuantityApplied, Is.EqualTo(5.5).Within(1e-9));
    }

    // ── Module setup packets ────────────────────────────────────────────────
    // Layouts pinned against the ESP32 firmware's own parser (Modules/ESP32
    // Rate/RC_ESP32/Receive.ino) — the authority on what the hardware accepts.
    // These carry settings the module keeps in EEPROM and that nothing else can
    // write: its web page is only WiFi + a master button.

    [Test]
    public void ControlSettings_MatchesFirmwareLayout()
    {
        var s = new RcControlSettings
        {
            MaxPwm = 90, MinPwm = 7, Kp = 40, Ki = 60, Deadband = 20, BrakePoint = 35,
            PidSlowAdjust = 60, SlewRate = 25, MaxIntegral = 250, TimedMinStart = 50,
            TimedAdjust = 80, TimedPause = 400, PidTime = 150,
            PulseMinHz = 10, PulseMaxHz = 1500, PulseSampleSize = 40,
        };
        var d = RcPgn.BuildControlSettings(moduleId: 2, sensorId: 1, s);

        Assert.That(d.Length, Is.EqualTo(24), "firmware PGNlength");
        Assert.That(d[0], Is.EqualTo(246));                 // id 32502, little-endian
        Assert.That(d[1], Is.EqualTo(126));
        Assert.That(d[0] | (d[1] << 8), Is.EqualTo(RcPgn.PGN_CONTROL_SETTINGS));
        Assert.That(d[2], Is.EqualTo(0x21));                // module 2, sensor 1
        Assert.That(d[3], Is.EqualTo(90));
        Assert.That(d[4], Is.EqualTo(7));
        Assert.That(d[5], Is.EqualTo(40));
        Assert.That(d[6], Is.EqualTo(60));
        Assert.That(d[7], Is.EqualTo(20));
        Assert.That(d[11], Is.EqualTo(250));
        Assert.That(d[12], Is.EqualTo(0), "byte 12 is spare");
        // 16-bit fields are little-endian: 400 = 0x0190
        Assert.That(d[14] | (d[15] << 8), Is.EqualTo(80), "TimedAdjust");
        Assert.That(d[16], Is.EqualTo(0x90));
        Assert.That(d[17], Is.EqualTo(0x01));
        Assert.That(d[20] | (d[21] << 8), Is.EqualTo(1500), "PulseMaxHz");
        Assert.That(d[22], Is.EqualTo(40));
        Assert.That(RcPgn.GoodCrc(d), Is.True);
    }

    [Test]
    public void SensorPins_MatchesFirmwareLayout()
    {
        var p = new RcSensorPins
        {
            FlowPin = 34, DirPin = 26, PwmPin = 27, BinPin = 255, InvertBinSensor = true,
        };
        var d = RcPgn.BuildSensorPins(moduleId: 0, sensorId: 3, p);

        Assert.That(d.Length, Is.EqualTo(11));
        Assert.That(d[0], Is.EqualTo(251));
        Assert.That(d[1], Is.EqualTo(126));
        Assert.That(d[0] | (d[1] << 8), Is.EqualTo(RcPgn.PGN_SENSOR_PINS));
        Assert.That(d[2], Is.EqualTo(0x03));
        Assert.That(d[3], Is.EqualTo(34));
        Assert.That(d[4], Is.EqualTo(26));
        Assert.That(d[5], Is.EqualTo(27));
        Assert.That(d[6], Is.EqualTo(255), "255 = no bin alarm");
        Assert.That(d[7] & 1, Is.EqualTo(1), "bit 0 inverts the bin sensor");
        Assert.That(RcPgn.GoodCrc(d), Is.True);
    }

    [Test]
    public void ModuleConfig_MatchesFirmwareLayout()
    {
        var c = new RcModuleConfig
        {
            ModuleId = 5, SensorCount = 2,
            InvertRelayControl = true, Is3WireValve = true, Ads1115Enabled = true,
            OnboardRelayType = 1, RemoteRelayType = 0,
            SensorPins = new[] { 34, 26, 27, 35, 32, 33 },
            RelayPins = new[] { 4, 5, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 23, 25, 2, 3 },
            WorkPin = 39, PressurePin = 36,
        };
        var d = RcPgn.BuildModuleConfig(c);

        Assert.That(d.Length, Is.EqualTo(33));
        Assert.That(d[0], Is.EqualTo(188));
        Assert.That(d[1], Is.EqualTo(127));
        Assert.That(d[0] | (d[1] << 8), Is.EqualTo(RcPgn.PGN_MODULE_CONFIG));
        Assert.That(d[2], Is.EqualTo(5));
        Assert.That(d[3], Is.EqualTo(2));
        Assert.That(d[4] & 1, Is.EqualTo(1), "bit 0 invert relay");
        Assert.That(d[4] & 2, Is.EqualTo(0), "bit 1 invert flow not set");
        Assert.That(d[4] & 16, Is.EqualTo(16), "bit 4 3-wire valve");
        Assert.That(d[4] & 32, Is.EqualTo(32), "bit 5 ADS1115");
        Assert.That(d[4] & 64, Is.EqualTo(0), "bit 6 clear = ID is a filter, not an assignment");
        Assert.That(d[7], Is.EqualTo(34));   // sensor 0 flow/dir/pwm
        Assert.That(d[9], Is.EqualTo(27));
        Assert.That(d[10], Is.EqualTo(35));  // sensor 1
        Assert.That(d[13], Is.EqualTo(4));   // relay pins 0-15 occupy 13..28
        Assert.That(d[28], Is.EqualTo(3));
        Assert.That(d[29], Is.EqualTo(39));
        Assert.That(d[30], Is.EqualTo(36));
        Assert.That(d[31], Is.EqualTo(0), "byte 31 is spare");
        Assert.That(RcPgn.GoodCrc(d), Is.True);
    }

    [Test]
    public void ModuleConfig_AssignIdSetsBit6()
    {
        // Commissioning: every module on the wire adopts this ID, so the UI must
        // insist on a single connected board before sending it.
        var d = RcPgn.BuildModuleConfig(new RcModuleConfig { ModuleId = 3, AssignModuleId = true });
        Assert.That(d[4] & 64, Is.EqualTo(64));
        Assert.That(d[2], Is.EqualTo(3));
        Assert.That(RcPgn.GoodCrc(d), Is.True);
    }

    [Test]
    public void ModuleConfig_ShortPinArraysDoNotOverrun()
    {
        // A partially-filled config must still produce a valid 33-byte frame.
        var d = RcPgn.BuildModuleConfig(new RcModuleConfig
        {
            ModuleId = 1,
            SensorPins = new[] { 34, 26 },
            RelayPins = new[] { 4 },
        });
        Assert.That(d.Length, Is.EqualTo(33));
        Assert.That(d[7], Is.EqualTo(34));
        Assert.That(d[9], Is.EqualTo(0), "unset pins stay zero");
        Assert.That(d[13], Is.EqualTo(4));
        Assert.That(RcPgn.GoodCrc(d), Is.True);
    }

    [Test]
    public void ControlSettings_DefaultsMatchAogRc()
    {
        // Defaults are AOG_RC's Props defaults, so a fresh setup behaves like the
        // Windows app rather than sending zeros to a valve.
        var d = RcPgn.BuildControlSettings(0, 0, new RcControlSettings());
        Assert.That(d[3], Is.EqualTo(100), "MaxPWM");
        Assert.That(d[4], Is.EqualTo(5), "MinPWM");
        Assert.That(d[5], Is.EqualTo(40), "Kp");
        Assert.That(d[6], Is.EqualTo(60), "Ki");
        Assert.That(d[8], Is.EqualTo(35), "BrakePoint");
        Assert.That(d[18], Is.EqualTo(150), "PIDtime");
        Assert.That(d[20] | (d[21] << 8), Is.EqualTo(1500), "PulseMaxHz");
        Assert.That(d[22], Is.EqualTo(40), "PulseSampleSize");
    }
}
