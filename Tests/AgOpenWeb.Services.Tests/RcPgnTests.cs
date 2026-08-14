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
}
