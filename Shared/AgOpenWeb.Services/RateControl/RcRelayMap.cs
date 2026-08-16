// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// What each of a module's 16 relay outputs actually DOES (the native app's
// Machine > Relays page), and the pure function that turns the machine's current
// state into the three 16-bit words PGN 32501 carries.
//
// Until now every relay was assumed to be section N — fine for a plain boom, but
// it left no way to wire the things a real implement needs: a master shutoff, a
// bypass valve that opens when no section is applying, tram markers, hydraulic
// lift, a geofence stop.
//
// Two of the words exist for a failure case rather than for normal running. The
// module holds a list of POWER relays and a list of INVERTED relays so it knows
// which outputs to drive when it stops hearing from us — an inverted relay
// (bypass, invert-master) is ON when the thing it mirrors is off, so leaving it
// de-energised on comms loss would be the wrong safe state.

using System.Collections.Generic;

namespace AgOpenWeb.Services.RateControl;

/// <summary>Relay functions, numbered as the native app stores them — the values
/// are persisted, so the odd order (None sitting at 11, in the middle) is kept
/// rather than tidied.</summary>
public enum RcRelayType : byte
{
    Section = 0,
    Slave = 1,
    Master = 2,
    Power = 3,
    InvertSection = 4,
    HydUp = 5,
    HydDown = 6,
    TramRight = 7,
    TramLeft = 8,
    GeoStop = 9,
    Switch = 10,
    None = 11,
    InvertMaster = 12,
    Bypass = 13,
    FlowMaster = 14,
    InvertFlowMaster = 15,
}

/// <summary>How the flow/master valve is driven through the relays. Distinct
/// from the module's own 2-wire/3-wire setting (PGN 32700 <c>Is3Wire</c>), which
/// describes the PRODUCT valve driver — this one is about the FlowMaster relay.</summary>
public enum RcFlowMasterMode : byte
{
    /// <summary>One relay signals; the valve is powered from its own supply and
    /// opens/closes itself. The usual case.</summary>
    ThreeWire = 0,
    /// <summary>One relay powers the valve both ways through the module's
    /// H-bridge. Needs module support (RC15); the relay's index is sent to the
    /// module so it knows to drive that output as a pair.</summary>
    TwoWire = 1,
    /// <summary>Two relays: FlowMaster powers open, Invert_FlowMaster powers
    /// close. For modules with no H-bridge (RC11-2). Nothing extra is sent — the
    /// pair of relay assignments is the whole mechanism.</summary>
    TwoWireInvertFlowMaster = 2,
}

/// <summary>One relay output's assignment.</summary>
public sealed class RcRelay
{
    public int Id { get; set; }                                  // 0-15
    public RcRelayType Type { get; set; } = RcRelayType.Section;
    /// <summary>Section this drives, for Section / Invert_Section (and tram
    /// markers that follow a section). -1 = none.</summary>
    public int SectionId { get; set; } = -1;
    /// <summary>Switchbox switch this follows, for the Switch type. 0-15.</summary>
    public int SwitchId { get; set; }
}

/// <summary>Everything the relay map needs to know about right now.</summary>
public readonly struct RcRelayInputs
{
    public bool MasterOn { get; init; }
    /// <summary>Auto section control is engaged, so flow should follow movement
    /// rather than the master switch alone.</summary>
    public bool AutoSectionOn { get; init; }
    public bool Moving { get; init; }
    /// <summary>A catch test is running: everything is forced on regardless of
    /// master, sections or speed, because the machine is stationary.</summary>
    public bool Calibrating { get; init; }
    public ulong SectionBits { get; init; }
    public ushort SwitchBits { get; init; }
    public bool TramLeft { get; init; }
    public bool TramRight { get; init; }
    public bool GeoStop { get; init; }
    public bool HydUp { get; init; }
    public bool HydDown { get; init; }
}

/// <summary>The three words and the valve index that make up PGN 32501.</summary>
public readonly struct RcRelayWords
{
    public ushort Relays { get; init; }
    public ushort Power { get; init; }
    public ushort Inverted { get; init; }
    /// <summary>Which relay the module should drive as a 2-wire pair, or 255.</summary>
    public byte FlowMasterIndex { get; init; }
}

public static class RcRelayMap
{
    public const int RelayCount = 16;
    public const byte NoFlowMasterValve = 255;

    /// <summary>A module's relays as they start out: relay N drives section N,
    /// counting on across modules the way the native app numbers them.</summary>
    public static List<RcRelay> DefaultRelays(int moduleId)
    {
        var list = new List<RcRelay>(RelayCount);
        for (int i = 0; i < RelayCount; i++)
            list.Add(new RcRelay { Id = i, Type = RcRelayType.Section, SectionId = moduleId * RelayCount + i });
        return list;
    }

    /// <summary>Give every Section / Invert_Section relay a consecutive section
    /// number from <paramref name="startSection"/> up. Anything that runs past
    /// the section count is switched off rather than left pointing at a section
    /// that does not exist.</summary>
    public static void Renumber(IReadOnlyList<RcRelay> relays, int startSection, int maxSections)
    {
        int section = startSection;
        foreach (var r in relays)
        {
            bool numbered = r.Type == RcRelayType.Section || r.Type == RcRelayType.InvertSection
                         || ((r.Type == RcRelayType.TramRight || r.Type == RcRelayType.TramLeft) && r.SectionId > 0);
            if (!numbered) continue;
            if (section < maxSections) r.SectionId = section++;
            else r.Type = RcRelayType.None;
        }
    }

    /// <summary>Turn the current machine state into the words PGN 32501 carries.
    /// Pure, so the wiring rules can be tested without a machine attached.</summary>
    public static RcRelayWords Compute(IReadOnlyList<RcRelay> relays, in RcRelayInputs inp,
        RcFlowMasterMode flowMasterMode)
    {
        bool masterOn = inp.MasterOn || inp.Calibrating;

        // With auto sections engaged, flow follows the machine actually moving —
        // otherwise product keeps going while stopped at the headland. Manual
        // mode leaves that to the operator's master switch.
        bool flowMasterOn = inp.Calibrating ? true
            : inp.AutoSectionOn ? inp.MasterOn && inp.Moving
            : inp.MasterOn;

        bool sectionsOn = inp.SectionBits != 0;

        ushort word = 0, power = 0, inverted = 0;
        int flowMasterId = -1, flowMasterCount = 0;

        foreach (var r in relays)
        {
            if (r.Id < 0 || r.Id >= RelayCount) continue;

            bool on = r.Type switch
            {
                RcRelayType.Master => masterOn,
                RcRelayType.InvertMaster => !masterOn,
                RcRelayType.FlowMaster => flowMasterOn,
                RcRelayType.InvertFlowMaster => !flowMasterOn,
                RcRelayType.Section => SectionOn(r.SectionId, inp) || inp.Calibrating,
                RcRelayType.InvertSection => r.SectionId >= 0 && !SectionOn(r.SectionId, inp),
                RcRelayType.Slave => sectionsOn,
                RcRelayType.Bypass => !sectionsOn,
                RcRelayType.Power => true,
                RcRelayType.Switch => (inp.SwitchBits & (1 << (r.SwitchId & 15))) != 0,
                RcRelayType.HydUp => inp.HydUp,
                RcRelayType.HydDown => inp.HydDown,
                RcRelayType.TramLeft => inp.TramLeft,
                RcRelayType.TramRight => inp.TramRight,
                RcRelayType.GeoStop => inp.GeoStop,
                _ => false,
            };
            if (on) word |= (ushort)(1 << r.Id);

            if (r.Type == RcRelayType.Power) power |= (ushort)(1 << r.Id);

            // What the module should hold on if we go quiet: anything whose
            // resting state is energised.
            if (r.Type is RcRelayType.InvertSection or RcRelayType.InvertMaster
                or RcRelayType.InvertFlowMaster or RcRelayType.Bypass)
                inverted |= (ushort)(1 << r.Id);

            if (r.Type == RcRelayType.FlowMaster) { flowMasterId = r.Id; flowMasterCount++; }
        }

        // Only meaningful with exactly one FlowMaster relay — two would leave the
        // module guessing which output to drive as a pair, so send nothing.
        byte valveIndex = (flowMasterMode == RcFlowMasterMode.TwoWire && flowMasterCount == 1)
            ? (byte)flowMasterId : NoFlowMasterValve;

        return new RcRelayWords
        {
            Relays = word, Power = power, Inverted = inverted, FlowMasterIndex = valveIndex,
        };
    }

    private static bool SectionOn(int sectionId, in RcRelayInputs inp) =>
        sectionId >= 0 && sectionId < 64 && (inp.SectionBits & (1UL << sectionId)) != 0;
}
