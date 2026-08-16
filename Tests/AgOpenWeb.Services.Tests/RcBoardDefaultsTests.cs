// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// The per-board factory defaults, checked against the RC sources they came from
// (RateController's frmMenuNetwork.SetDefaults and the module firmware's
// LoadDefaults). Nothing can read a module's settings back over the wire, so if
// this table is wrong the operator is handed plausible pin numbers that quietly
// meter nothing — worth pinning down away from the hardware.

using AgOpenWeb.Services.RateControl;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class RcBoardDefaultsTests
{
    private static RcModuleSetup Fresh(int id = 0) =>
        new() { ModuleId = id, Sensors = { new RcSensorSetup { SensorId = 0 } } };

    [Test]
    public void Esp32_MatchesRc15PinMap()
    {
        var m = Fresh();
        RcBoardDefaults.Apply(m, "esp32");

        var s0 = m.Sensors.Find(s => s.SensorId == 0)!;
        Assert.Multiple(() =>
        {
            Assert.That(s0.Pins.FlowPin, Is.EqualTo(17));
            Assert.That(s0.Pins.DirPin, Is.EqualTo(32));
            Assert.That(s0.Pins.PwmPin, Is.EqualTo(33));
            Assert.That(m.Config.OnboardRelayType, Is.EqualTo(5), "PCA9685");
            Assert.That(m.Config.Ads1115Enabled, Is.True);
            Assert.That(m.Config.WorkPin, Is.EqualTo(255));
            Assert.That(m.Config.PressurePin, Is.EqualTo(255));
        });
        // Relays go through the PCA9685 by channel, so no relay owns a micro pin.
        Assert.That(m.Config.RelayPins, Is.All.EqualTo(255));
    }

    [Test]
    public void Teensy_MatchesRc11Dash2PinMap()
    {
        var m = Fresh();
        RcBoardDefaults.Apply(m, "teensy");

        var s0 = m.Sensors.Find(s => s.SensorId == 0)!;
        Assert.Multiple(() =>
        {
            Assert.That(s0.Pins.FlowPin, Is.EqualTo(28));
            Assert.That(s0.Pins.DirPin, Is.EqualTo(37));
            Assert.That(s0.Pins.PwmPin, Is.EqualTo(36));
            Assert.That(m.Config.OnboardRelayType, Is.EqualTo(1), "straight off micro pins");
            Assert.That(m.Config.Ads1115Enabled, Is.False);
            Assert.That(m.Config.WorkPin, Is.EqualTo(30));
            Assert.That(m.Config.PressurePin, Is.EqualTo(40));
        });
        // Eight relays wired, the high bank unused.
        Assert.That(m.Config.RelayPins[..8], Is.EqualTo(new byte[] { 8, 9, 10, 11, 12, 25, 26, 27 }));
        Assert.That(m.Config.RelayPins[8..], Is.All.EqualTo(255));
    }

    [Test]
    public void Nano_MatchesRc12Dash3PinMap()
    {
        var m = Fresh();
        RcBoardDefaults.Apply(m, "nano");

        var s0 = m.Sensors.Find(s => s.SensorId == 0)!;
        Assert.Multiple(() =>
        {
            Assert.That(s0.Pins.FlowPin, Is.EqualTo(3));
            Assert.That(s0.Pins.DirPin, Is.EqualTo(4));
            Assert.That(s0.Pins.PwmPin, Is.EqualTo(5));
            Assert.That(m.Config.OnboardRelayType, Is.EqualTo(4), "MCP23017");
            // A0 and A1 on a Nano — the analog pins keep counting from 14.
            Assert.That(m.Config.PressurePin, Is.EqualTo(14));
            Assert.That(m.Config.WorkPin, Is.EqualTo(15));
        });
        Assert.That(m.Config.RelayPins,
            Is.EqualTo(new byte[] { 0, 15, 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
    }

    [Test]
    public void SecondSensorGetsPins_OnlyWhenTheModuleHasTwo()
    {
        var one = Fresh();
        RcBoardDefaults.Apply(one, "esp32");
        Assert.That(one.Sensors, Has.Count.EqualTo(1), "a one-channel module gains no second sensor");

        var two = Fresh();
        two.Config.SensorCount = 2;
        RcBoardDefaults.Apply(two, "esp32");
        var s1 = two.Sensors.Find(s => s.SensorId == 1);
        Assert.That(s1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(s1!.Pins.FlowPin, Is.EqualTo(16));
            Assert.That(s1.Pins.DirPin, Is.EqualTo(25));
            Assert.That(s1.Pins.PwmPin, Is.EqualTo(26));
        });
    }

    [Test]
    public void UnknownBoard_FallsBackToEsp32_AndRecordsThat()
    {
        var m = Fresh();
        RcBoardDefaults.Apply(m, "wombat");
        Assert.That(m.Board, Is.EqualTo("esp32"), "never store a board name nothing can act on");
        Assert.That(m.Sensors[0].Pins.FlowPin, Is.EqualTo(17));
    }

    [Test]
    public void DefaultsAreThreeWire_AndFlowSampleCountIsWithinTheModuleCap()
    {
        var m = Fresh();
        RcBoardDefaults.Apply(m, "esp32");
        // 3-wire is the default wiring for most installations; the invert-flow
        // flag beside it is direction, not a wiring mode, and the firmware also
        // defaults it on.
        Assert.That(m.Config.Is3WireValve, Is.True);
        Assert.That(m.Config.InvertFlowControl, Is.True);
        // A COUNT of pulse times, not a duration. Both the firmware and the
        // native app default it to 12; the module caps it at MaxSampleSize, so
        // anything above 25 would silently not be what was sent. (One ESP32 PCB
        // variant caps at 11 and trims 12 down — harmless, and still the value
        // the RC itself ships.)
        Assert.That(m.Sensors[0].Control.PulseSampleSize, Is.EqualTo(12));
    }

    [Test]
    public void ApplyingTwice_LeavesNoStaleRelayPins()
    {
        var m = Fresh();
        RcBoardDefaults.Apply(m, "nano");
        RcBoardDefaults.Apply(m, "esp32");
        Assert.That(m.Config.RelayPins, Is.All.EqualTo(255),
            "switching from a pin-driven board must clear its relay map");
    }
}
