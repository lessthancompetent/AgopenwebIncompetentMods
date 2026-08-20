// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Physical switchbox (PGN 32618) — frame layout pinned against AOG_RC's
// PGN32618.ParseByteData (the box's Arduino sketch mirrors it): header 106 127,
// status bits in byte 2, 16 maintained section switches in bytes 3-4, optional
// InoID trailer, additive CRC from byte 0 in the LAST byte. The buttons are
// momentary, so the edge bookkeeping (act once per press, never on a held or
// stuck bit) is what keeps a chemical master safe — tested hard here.

using System;
using AgOpenWeb.Services.RateControl;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class RcSwitchbox32618Tests
{
    // Status-byte bits (bit 0 unused).
    private const byte MasterOn = 1 << 1;
    private const byte MasterOff = 1 << 2;
    private const byte RateUp = 1 << 3;
    private const byte RateDown = 1 << 4;
    private const byte AutoSection = 1 << 5;
    private const byte AutoRate = 1 << 6;
    private const byte Work = 1 << 7;

    /// <summary>Build a wire frame: 6-byte short form, or the longer form with
    /// the InoID trailer when an id is given.</summary>
    private static byte[] Frame(byte status, byte swLo = 0, byte swHi = 0, int? inoId = null)
    {
        var d = inoId is int id
            ? new byte[] { 106, 127, status, swLo, swHi, (byte)id, (byte)(id >> 8), 0, 0 }
            : new byte[] { 106, 127, status, swLo, swHi, 0 };
        d[^1] = RcPgn.Crc(d, d.Length - 1);
        return d;
    }

    // ── Frame parsing ───────────────────────────────────────────────────────

    [Test]
    public void Parse_ShortFrame_MapsEveryStatusBit()
    {
        Assert.That(RcPgn.TryParseSwitchbox(
            Frame((byte)(MasterOn | RateDown | AutoRate | Work)), out var f), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(f.MasterOnPressed, Is.True);
            Assert.That(f.MasterOffPressed, Is.False);
            Assert.That(f.RateUpPressed, Is.False);
            Assert.That(f.RateDownPressed, Is.True);
            Assert.That(f.AutoSectionOn, Is.False);
            Assert.That(f.AutoRateOn, Is.True);
            Assert.That(f.WorkSwitchOn, Is.True);
            Assert.That(f.InoId, Is.Zero, "short form carries no id");
        });
    }

    [Test]
    public void Parse_SectionBytes_MapLowByteToSections1To8()
    {
        // Switches 1, 3 and 9 up → byte3 bit0+bit2, byte4 bit0.
        Assert.That(RcPgn.TryParseSwitchbox(
            Frame(0, swLo: 0b0000_0101, swHi: 0b0000_0001), out var f), Is.True);
        Assert.That(f.SectionBits, Is.EqualTo(0b1_0000_0101));
    }

    [Test]
    public void Parse_LongFrame_CarriesInoId()
    {
        Assert.That(RcPgn.TryParseSwitchbox(Frame(Work, inoId: 0x621B), out var f), Is.True);
        Assert.That(f.InoId, Is.EqualTo(0x621B));
        Assert.That(f.WorkSwitchOn, Is.True);
    }

    [Test]
    public void Parse_RejectsBadCrc_WrongHeader_AndShortRunts()
    {
        var bad = Frame(MasterOn);
        bad[2] ^= 0xFF;   // corrupt after the CRC was computed
        Assert.That(RcPgn.TryParseSwitchbox(bad, out _), Is.False, "corrupted frame must fail CRC");

        var wrongHdr = Frame(MasterOn);
        wrongHdr[1] = 126;
        Assert.That(RcPgn.TryParseSwitchbox(wrongHdr, out _), Is.False, "not 106/127");

        Assert.That(RcPgn.TryParseSwitchbox(new byte[] { 106, 127, 0, 0, 233 }, out _),
            Is.False, "5 bytes is a runt even with a valid sum");
    }

    // ── Momentary edge behaviour ────────────────────────────────────────────

    private static RcSwitchboxActions Apply(RcPhysicalSwitchbox box, byte status,
        DateTime at, byte swLo = 0, byte swHi = 0, bool maintained = false)
    {
        Assert.That(RcPgn.TryParseSwitchbox(Frame(status, swLo, swHi), out var f), Is.True);
        return box.Apply(f, at, maintained);
    }

    [Test]
    public void MasterOn_ActsOnRisingEdgeOnly()
    {
        var box = new RcPhysicalSwitchbox();
        var t = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

        Assert.That(Apply(box, 0, t).SetMaster, Is.Null, "idle frame does nothing");
        Assert.That(Apply(box, MasterOn, t.AddSeconds(0.2)).SetMaster, Is.True, "press edge");
        Assert.That(Apply(box, MasterOn, t.AddSeconds(0.4)).SetMaster, Is.Null, "held, not a new press");
        Assert.That(Apply(box, 0, t.AddSeconds(0.6)).SetMaster, Is.Null, "release is not an action");
        Assert.That(Apply(box, MasterOn, t.AddSeconds(0.8)).SetMaster, Is.True, "second press acts again");
    }

    [Test]
    public void MasterOff_EdgeStops_AndBeatsSimultaneousOn()
    {
        var box = new RcPhysicalSwitchbox();
        var t = DateTime.UtcNow;
        Apply(box, 0, t);
        Assert.That(Apply(box, MasterOff, t.AddSeconds(0.2)).SetMaster, Is.False);
        // Both bits rising in one frame: land on OFF — when in doubt, stop metering.
        Apply(box, 0, t.AddSeconds(0.4));
        Assert.That(Apply(box, (byte)(MasterOn | MasterOff), t.AddSeconds(0.6)).SetMaster, Is.False);
    }

    [Test]
    public void FirstFrame_WithHeldButtons_FiresNothingMomentary()
    {
        // A button already down when the box appears is a held (or stuck)
        // button, not a press.
        var box = new RcPhysicalSwitchbox();
        var a = Apply(box, (byte)(MasterOn | RateUp | RateDown), DateTime.UtcNow);
        Assert.Multiple(() =>
        {
            Assert.That(a.SetMaster, Is.Null);
            Assert.That(a.RateUpEdge, Is.False);
            Assert.That(a.RateDownEdge, Is.False);
        });
    }

    [Test]
    public void RateButtons_EdgePerPress()
    {
        var box = new RcPhysicalSwitchbox();
        var t = DateTime.UtcNow;
        Apply(box, 0, t);
        Assert.That(Apply(box, RateUp, t.AddSeconds(0.2)).RateUpEdge, Is.True);
        Assert.That(Apply(box, RateUp, t.AddSeconds(0.4)).RateUpEdge, Is.False, "held");
        var a = Apply(box, RateDown, t.AddSeconds(0.6));
        Assert.That(a.RateUpEdge, Is.False);
        Assert.That(a.RateDownEdge, Is.True, "up released, down pressed, one frame");
    }

    [Test]
    public void MaintainedMasterType_FollowsTheLevel()
    {
        // Switch type "maintained": the MasterOn bit is a switch POSITION
        // (AOG_RC's MasterMaintained workaround) — applied on first contact and
        // on every move, and MasterOff is ignored.
        var box = new RcPhysicalSwitchbox();
        var t = DateTime.UtcNow;
        Assert.That(Apply(box, MasterOn, t, maintained: true).SetMaster, Is.True,
            "position imposed on first frame");
        Assert.That(Apply(box, MasterOn, t.AddSeconds(0.2), maintained: true).SetMaster, Is.Null);
        Assert.That(Apply(box, 0, t.AddSeconds(0.4), maintained: true).SetMaster, Is.False);
        Assert.That(Apply(box, MasterOff, t.AddSeconds(0.6), maintained: true).SetMaster,
            Is.Null, "MasterOff bit means nothing to a maintained switch");
    }

    // ── Auto toggles (maintained) ───────────────────────────────────────────

    [Test]
    public void AutoToggles_ActOnMoveOnly_NeverOnReconnect()
    {
        var box = new RcPhysicalSwitchbox();
        var t = DateTime.UtcNow;

        // First frame reports positions but imposes nothing: a box without
        // these toggles wired (bits stuck 0) must not switch modes off.
        var a = Apply(box, 0, t);
        Assert.That(a.SetAutoSection, Is.Null);
        Assert.That(a.SetAutoRate, Is.Null);

        a = Apply(box, (byte)(AutoSection | AutoRate), t.AddSeconds(0.2));
        Assert.That(a.SetAutoSection, Is.True, "toggle moved up");
        Assert.That(a.SetAutoRate, Is.True);
        Assert.That(box.AutoSectionOn, Is.True);

        a = Apply(box, (byte)(AutoSection | AutoRate), t.AddSeconds(0.4));
        Assert.That(a.SetAutoSection, Is.Null, "steady level re-imposes nothing");

        a = Apply(box, AutoRate, t.AddSeconds(0.6));
        Assert.That(a.SetAutoSection, Is.False, "toggle moved down");
        Assert.That(a.SetAutoRate, Is.Null);

        // Reconnect after a gap with the same levels: still no action.
        a = Apply(box, AutoRate, t.AddSeconds(30));
        Assert.That(a.SetAutoSection, Is.Null);
        Assert.That(a.SetAutoRate, Is.Null);
    }

    // ── Connection window ───────────────────────────────────────────────────

    [Test]
    public void Connected_TracksTheFourSecondWindow()
    {
        var box = new RcPhysicalSwitchbox();
        var t = DateTime.UtcNow;
        Assert.That(box.Connected(t), Is.False, "never heard");

        Apply(box, Work, t);
        Assert.That(box.Connected(t.AddSeconds(3.9)), Is.True);
        Assert.That(box.Connected(t.AddSeconds(4.1)), Is.False, "silent past 4 s");
        Assert.That(box.WorkSwitchOn, Is.True, "last levels stay readable while stale");
    }

    [Test]
    public void ReconnectAfterGap_TreatsHeldButtonsAsHeld()
    {
        var box = new RcPhysicalSwitchbox();
        var t = DateTime.UtcNow;
        Apply(box, 0, t);
        // The box vanishes >4 s and comes back with MasterOn already down —
        // that is a held button across an outage, not a fresh press.
        Assert.That(Apply(box, MasterOn, t.AddSeconds(10)).SetMaster, Is.Null);
        // ...but a genuine press after the reconnect works.
        Apply(box, 0, t.AddSeconds(10.2));
        Assert.That(Apply(box, MasterOn, t.AddSeconds(10.4)).SetMaster, Is.True);
    }

    // ── Section-switch gating ───────────────────────────────────────────────

    [Test]
    public void SectionGateMask_DefaultAllocation_SectionFollowsOwnSwitch()
    {
        var s = new RcSwitchboxSettings();   // section N → switch N, 8+ → switch 7
        // Switches 1 and 3 up (bits 0, 2).
        ushort mask = RcPhysicalSwitchbox.SectionGateMask(s, 0b0000_0101);
        Assert.That(mask, Is.EqualTo(0b0000_0101), "sections 1 and 3 allowed, rest vetoed");

        // Switch 8 up gates every section past the eighth too (they share it).
        mask = RcPhysicalSwitchbox.SectionGateMask(s, 0b1000_0000);
        Assert.That(mask, Is.EqualTo(0b1111_1111_1000_0000));
    }

    [Test]
    public void SectionGateMask_GroupedAndUnallocated()
    {
        // Sections 1-2 grouped on switch 3; section 3 unallocated (never gated).
        var s = new RcSwitchboxSettings { SectionSwitch = new[] { 2, 2, -1 } };
        Assert.That(RcPhysicalSwitchbox.SectionGateMask(s, 0), Is.EqualTo(0b0100),
            "all switches down: only the unallocated section survives");
        Assert.That(RcPhysicalSwitchbox.SectionGateMask(s, 0b0100) & 0b0111, Is.EqualTo(0b0111),
            "switch 3 up frees the whole group");
    }
}
