// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Threading;
using AgOpenWeb.Models.Configuration;
using AgOpenWeb.Models.State;
using AgOpenWeb.Services;
using AgOpenWeb.Services.AutoSteer;
using AgOpenWeb.Services.Interfaces;
using AgOpenWeb.Services.Track;
using NSubstitute;

namespace AgOpenWeb.Services.Tests;

/// <summary>
/// Pins the automatic machine-module config emission (PGN 238 machine
/// config, 236 relay pin map, 235 section dimensions): a baseline at Start,
/// a debounced re-emit on machine / section-layout changes, and a re-push
/// when the machine board's hello reappears. Before this the trio only ever
/// left on the manual Send button, so a rebooted board ran on stale EEPROM.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class MachineConfigEmitTests
{
    private IUdpCommunicationService _udp = null!;
    private AutoSteerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        _udp = Substitute.For<IUdpCommunicationService>();
        _service = new AutoSteerService(
            Substitute.For<ITrackGuidanceService>(), _udp, Substitute.For<IGpsService>(),
            new ApplicationState(), ConfigurationStore.Instance);
        _service.ConfigEmitDebounceMilliseconds = 20;
        _service.Start();
    }

    [TearDown]
    public void TearDown() => _service.Stop();

    private static bool IsPgn(byte[] p, byte pgn) => p.Length > 4 && p[3] == pgn;

    private void AssertTrioEmittedWithin(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEE))); // 238
                _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEC))); // 236
                _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEB))); // 235
                return;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException) { Thread.Sleep(10); }
        }
        _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEE)));
        _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEC)));
        _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEB)));
    }

    [Test]
    public void Start_EmitsMachineTrioBaseline()
    {
        // Start() ran in SetUp — the baseline must already be on the wire.
        AssertTrioEmittedWithin(100);
    }

    [Test]
    public void MachineConfigChange_ReemitsTrio_Debounced()
    {
        _udp.ClearReceivedCalls();
        ConfigurationStore.Instance.Machine.RaiseTime = 7;
        ConfigurationStore.Instance.Machine.SetPinAssignment(3, PinFunction.HydUp);
        AssertTrioEmittedWithin(500);
        // Two rapid edits coalesce into ONE 238 emission (debounce).
        Thread.Sleep(150);
        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEE)));
    }

    [Test]
    public void SectionWidthChange_ReemitsTrio()
    {
        _udp.ClearReceivedCalls();
        ConfigurationStore.Instance.Tool.SetSectionWidth(0, 250);
        AssertTrioEmittedWithin(500);
    }

    [Test]
    public void MachineModuleReconnect_ReemitsTrio()
    {
        _udp.ClearReceivedCalls();
        _udp.ModuleConnectionChanged += Raise.EventWith(_udp, new ModuleConnectionEventArgs
        { ModuleType = ModuleType.Machine, IsConnected = true, IPAddress = "192.168.5.123" });
        AssertTrioEmittedWithin(500);
        // The steer pair must NOT ride along on a machine reconnect.
        Thread.Sleep(100);
        _udp.DidNotReceive().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFB)));
    }

    [Test]
    public void SteerModuleReconnect_DoesNotEmitMachineTrio()
    {
        _udp.ClearReceivedCalls();
        _udp.ModuleConnectionChanged += Raise.EventWith(_udp, new ModuleConnectionEventArgs
        { ModuleType = ModuleType.AutoSteer, IsConnected = true, IPAddress = "192.168.5.126" });
        Thread.Sleep(250);
        _udp.DidNotReceive().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xEE)));
    }

    /// <summary>Byte layout pinned against stock Machine_UDP_v5.ino: [5] raise,
    /// [6] lower, [7] lift enable, [8] set0 bit0 = relay active-high (Invert),
    /// [9..12] user1-4; 236 = 24 pin bytes from [5].</summary>
    [Test]
    public void Pgn238And236_MatchStockFirmwareLayout()
    {
        var m = ConfigurationStore.Instance.Machine;
        m.RaiseTime = 3; m.LowerTime = 4; m.HydraulicLiftEnabled = true; m.InvertRelay = true;
        m.User1Value = 11; m.User4Value = 44;
        m.SetPinAssignment(0, PinFunction.Section1);
        m.SetPinAssignment(23, PinFunction.HydDown);

        var p238 = PgnBuilder.BuildMachineConfigPgn(m);
        Assert.That(p238[3], Is.EqualTo(0xEE));
        Assert.That(p238[5], Is.EqualTo(3));
        Assert.That(p238[6], Is.EqualTo(4));
        Assert.That(p238[7], Is.EqualTo(1));
        Assert.That(p238[8] & 0x01, Is.EqualTo(1), "set0 bit0 = relay active-high");
        Assert.That(p238[9], Is.EqualTo(11));
        Assert.That(p238[12], Is.EqualTo(44));

        var p236 = PgnBuilder.BuildMachinePinsPgn(m);
        Assert.That(p236[3], Is.EqualTo(0xEC));
        Assert.That(p236[4], Is.EqualTo(24));
        Assert.That(p236[5], Is.EqualTo((byte)PinFunction.Section1));
        Assert.That(p236[5 + 23], Is.EqualTo((byte)PinFunction.HydDown));
    }
}
