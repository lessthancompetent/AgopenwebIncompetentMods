// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// State for a PHYSICAL AOG_RC switchbox (PGN 32618) working alongside the
// on-screen one. The box streams its switch levels several times a second;
// "connected" means frames still arrive (AOG_RC's own 4 s window). Master,
// rate up and rate down are MOMENTARY buttons whose bit is the held level, so
// acting on the level would re-fire every frame — actions come from 0→1 edges
// only. Auto section / auto rate / work / section switches are maintained
// positions: work and sections are read live, the two auto toggles act when
// they MOVE (never on mere reconnect, so a box without those toggles wired —
// bits stuck at 0 — cannot silently switch the modes off every time it
// reappears).

using System;

namespace AgOpenWeb.Services.RateControl;

/// <summary>What one frame asks the app to DO (edges and level changes), kept
/// apart from the levels it reports so the caller's side effects stay out of
/// the edge bookkeeping.</summary>
public readonly struct RcSwitchboxActions
{
    /// <summary>Set the master: a momentary MasterOn/MasterOff press edge, or
    /// the maintained switch level moving. Null = leave it alone.</summary>
    public bool? SetMaster { get; init; }
    public bool RateUpEdge { get; init; }
    public bool RateDownEdge { get; init; }
    /// <summary>Auto-section toggle moved; the value is its new position.</summary>
    public bool? SetAutoSection { get; init; }
    /// <summary>Auto-rate toggle moved; the value is its new position.</summary>
    public bool? SetAutoRate { get; init; }
}

public sealed class RcPhysicalSwitchbox
{
    /// <summary>No frame for this long → disconnected (AOG_RC Connected()).</summary>
    public const double TimeoutSeconds = 4;

    private RcSwitchboxFrame _last;
    private bool _fresh = true;   // next frame is the first after (re)connect

    public DateTime LastFrameUtc { get; private set; } = DateTime.MinValue;
    public bool WorkSwitchOn { get; private set; }
    public bool AutoSectionOn { get; private set; }
    public bool AutoRateOn { get; private set; }
    /// <summary>The 16 maintained section switch levels, bit 0 = switch 1.</summary>
    public ushort SectionBits { get; private set; }
    public int InoId { get; private set; }

    public bool Connected(DateTime nowUtc) => (nowUtc - LastFrameUtc).TotalSeconds < TimeoutSeconds;

    /// <summary>Fold one received frame in and report what it asks for. A gap
    /// long enough to have read as "disconnected" clears the edge history: a
    /// button already down on the first frame back is a HELD button, not a
    /// press, so nothing momentary fires from it.</summary>
    public RcSwitchboxActions Apply(in RcSwitchboxFrame f, DateTime nowUtc, bool masterMaintained)
    {
        bool fresh = _fresh || !Connected(nowUtc);
        var prev = _last;
        _last = f;
        _fresh = false;
        LastFrameUtc = nowUtc;
        WorkSwitchOn = f.WorkSwitchOn;
        AutoSectionOn = f.AutoSectionOn;
        AutoRateOn = f.AutoRateOn;
        SectionBits = f.SectionBits;
        if (f.InoId != 0) InoId = f.InoId;

        bool? master = null;
        if (masterMaintained)
        {
            // Maintained master (Switches tab, switch type): the MasterOn bit is
            // a switch POSITION, followed on any move and on (re)connect —
            // AOG_RC's MasterMaintained workaround, where MasterOff is derived
            // and ignored.
            if (fresh || f.MasterOnPressed != prev.MasterOnPressed)
                master = f.MasterOnPressed;
        }
        else
        {
            // Momentary: edges only. Both pressed in one frame lands on OFF —
            // when in doubt, stop metering.
            if (!fresh && f.MasterOnPressed && !prev.MasterOnPressed) master = true;
            if (!fresh && f.MasterOffPressed && !prev.MasterOffPressed) master = false;
        }

        return new RcSwitchboxActions
        {
            SetMaster = master,
            RateUpEdge = !fresh && f.RateUpPressed && !prev.RateUpPressed,
            RateDownEdge = !fresh && f.RateDownPressed && !prev.RateDownPressed,
            SetAutoSection = !fresh && f.AutoSectionOn != prev.AutoSectionOn
                ? f.AutoSectionOn : null,
            SetAutoRate = !fresh && f.AutoRateOn != prev.AutoRateOn
                ? f.AutoRateOn : null,
        };
    }

    /// <summary>Which sections the box's switches allow. Section N follows the
    /// switch its allocation names — the same allocation the on-screen box
    /// groups by — and an unallocated section (-1) is not gated at all. The
    /// switch can only VETO a section, never force one on: coverage and auto
    /// section stay authoritative for what should be applying.</summary>
    public static ushort SectionGateMask(RcSwitchboxSettings settings, ushort switchBits)
    {
        ushort mask = 0;
        for (int i = 0; i < 16; i++)
        {
            int sw = settings.SwitchFor(i);
            if (sw < 0 || sw > 15 || (switchBits & 1 << sw) != 0)
                mask |= (ushort)(1 << i);
        }
        return mask;
    }
}
