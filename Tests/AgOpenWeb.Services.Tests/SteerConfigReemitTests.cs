// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Net;
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
/// Pins the steer-config re-emit on module hello: a steer module that
/// (re)appears after the hello timeout may have rebooted with blank or
/// stale EEPROM, and the app's startup PGN 251/252 emission goes to nobody
/// when the app boots before the module powers up. The profile is the
/// source of truth — the module must be re-programmed automatically on
/// every (re)connect, without the operator touching a setting.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class SteerConfigReemitTests
{
    private IUdpCommunicationService _udp = null!;
    private AutoSteerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _udp = Substitute.For<IUdpCommunicationService>();
        var guidance = Substitute.For<ITrackGuidanceService>();
        var gps = Substitute.For<IGpsService>();
        var appState = new ApplicationState();
        _service = new AutoSteerService(guidance, _udp, gps, appState, ConfigurationStore.Instance);
        _service.ConfigEmitDebounceMilliseconds = 20;
        _service.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _service.Stop();
    }

    private static bool IsPgn(byte[] packet, byte pgn) =>
        packet.Length > 4 && packet[3] == pgn;

    [Test]
    public void SteerModuleReconnect_ReemitsConfigPair()
    {
        // Start() already emitted the startup baseline — discard it so the
        // assertion below can only be satisfied by the reconnect path.
        _udp.ClearReceivedCalls();

        _udp.ModuleConnectionChanged += Raise.EventWith(_udp, new ModuleConnectionEventArgs
        {
            ModuleType = ModuleType.AutoSteer,
            IsConnected = true,
            IPAddress = "192.168.5.126"
        });

        // Debounced emission (20 ms in tests) — poll rather than sleep a
        // fixed wall-clock amount.
        AssertEmittedWithin(500);
    }

    [Test]
    public void MachineModuleReconnect_DoesNotEmitSteerConfig()
    {
        _udp.ClearReceivedCalls();

        _udp.ModuleConnectionChanged += Raise.EventWith(_udp, new ModuleConnectionEventArgs
        {
            ModuleType = ModuleType.Machine,
            IsConnected = true,
            IPAddress = "192.168.5.123"
        });

        Thread.Sleep(250);
        _udp.DidNotReceive().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFB)));
        _udp.DidNotReceive().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFC)));
    }

    private void AssertEmittedWithin(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFB))); // 251
                _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFC))); // 252
                return;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                Thread.Sleep(10);
            }
        }
        // Final assert surfaces the NSubstitute diagnostic on failure.
        _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFB)));
        _udp.Received().SendToModules(Arg.Is<byte[]>(b => IsPgn(b, 0xFC)));
    }
}

/// <summary>
/// Pins the connect-transition detection in <see cref="UdpCommunicationService"/>:
/// the ModuleConnectionChanged event fires when a module's hello arrives
/// while the module was considered gone (first contact after app start, or
/// hello resuming after the 2 s timeout) — and does NOT fire again on the
/// steady ~1 Hz hello stream. The 2 s-gap re-fire uses the same wall-clock
/// comparison as first contact; DateTime.Now isn't injectable, so the gap
/// case itself is bench-verified rather than unit-tested.
/// </summary>
[TestFixture]
public class UdpModuleReconnectEventTests
{
    private static byte[] BuildHello(byte pgn)
    {
        // UpdateModuleConnection only reads data[3]; keep the frame wire-shaped.
        return new byte[] { 0x80, 0x81, pgn, pgn, 0, 0 };
    }

    [Test]
    public void FirstHello_RaisesConnected_SteadyHellosDoNot()
    {
        var netInfo = Substitute.For<ILocalNetworkInfoProvider>();
        var svc = new UdpCommunicationService(netInfo);

        int steerConnects = 0;
        ModuleConnectionEventArgs? last = null;
        svc.ModuleConnectionChanged += (_, e) =>
        {
            if (e.ModuleType == ModuleType.AutoSteer && e.IsConnected)
            {
                steerConnects++;
                last = e;
            }
        };

        var from = new IPEndPoint(IPAddress.Parse("192.168.5.126"), 5126);
        var hello = BuildHello(126); // HELLO_FROM_AUTOSTEER

        svc.UpdateModuleConnection(hello, from);
        Assert.That(steerConnects, Is.EqualTo(1), "first hello after start = connect transition");
        Assert.That(last!.IPAddress, Is.EqualTo("192.168.5.126"));

        // Steady stream: hellos arriving well inside the 2 s timeout must
        // not re-fire (the module never went away).
        svc.UpdateModuleConnection(hello, from);
        svc.UpdateModuleConnection(hello, from);
        Assert.That(steerConnects, Is.EqualTo(1), "steady hellos must not re-raise");
    }

    [Test]
    public void MachineHello_RaisesMachineConnect_NotSteer()
    {
        var netInfo = Substitute.For<ILocalNetworkInfoProvider>();
        var svc = new UdpCommunicationService(netInfo);

        int steerConnects = 0, machineConnects = 0;
        svc.ModuleConnectionChanged += (_, e) =>
        {
            if (!e.IsConnected) return;
            if (e.ModuleType == ModuleType.AutoSteer) steerConnects++;
            if (e.ModuleType == ModuleType.Machine) machineConnects++;
        };

        var from = new IPEndPoint(IPAddress.Parse("192.168.5.123"), 5123);
        svc.UpdateModuleConnection(BuildHello(123), from); // HELLO_FROM_MACHINE

        Assert.That(machineConnects, Is.EqualTo(1));
        Assert.That(steerConnects, Is.EqualTo(0));
    }
}
