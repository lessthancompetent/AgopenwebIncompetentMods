// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// The machine board's work-switch report (PGN 237 data byte 3): bit 7 says the
// firmware has a work switch at all, bit 0 is the raw state. The flag matters
// because STOCK machine firmware leaves the byte at zero - without it, every
// stock board would read as "work switch present, off" and the work-switch
// gate would hold the master off forever.

using AgOpenWeb.Services.AutoSteer;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class MachineWorkSwitchTests
{
    private static byte[] Frame(byte switchByte)
    {
        // 0x80 0x81 src pgn len d0..d7 crc — FromMachine (0xED), 8 data bytes
        var f = new byte[] { 0x80, 0x81, 0x7F, 0xED, 8, 0, 0, 0, switchByte, 0, 0, 0, 0, 0 };
        int ck = 0; for (int i = 2; i < f.Length - 1; i++) ck += f[i];
        f[^1] = (byte)ck;
        return f;
    }

    [Test]
    public void ExtendedFirmware_ReportsPresentAndState()
    {
        Assert.That(PgnBuilder.TryParseMachineWorkSwitch(Frame(0x81), out var p1, out var a1), Is.True);
        Assert.Multiple(() => { Assert.That(p1, Is.True); Assert.That(a1, Is.True, "switch closed"); });

        Assert.That(PgnBuilder.TryParseMachineWorkSwitch(Frame(0x80), out var p2, out var a2), Is.True);
        Assert.Multiple(() => { Assert.That(p2, Is.True); Assert.That(a2, Is.False, "switch open"); });
    }

    [Test]
    public void StockFirmware_ZeroByte_MeansNoSwitchNotOffSwitch()
    {
        Assert.That(PgnBuilder.TryParseMachineWorkSwitch(Frame(0x00), out var present, out _), Is.True);
        Assert.That(present, Is.False,
            "a stock board must not read as a phantom always-off work switch");
    }

    [Test]
    public void WrongPgnOrShortFrame_Rejected()
    {
        var steer = Frame(0x81); steer[3] = 0xFD;   // a 253 frame
        Assert.That(PgnBuilder.TryParseMachineWorkSwitch(steer, out _, out _), Is.False);
        Assert.That(PgnBuilder.TryParseMachineWorkSwitch(new byte[] { 0x80, 0x81, 0x7F, 0xED }, out _, out _), Is.False);
    }
}
