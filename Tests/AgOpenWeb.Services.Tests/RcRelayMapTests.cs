// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Relay function assignment: what each of a module's 16 outputs does, and the
// three words PGN 32501 carries.
//
// Worth pinning down away from a machine because getting these backwards has
// physical consequences — a bypass valve that shuts when it should divert, or a
// relay map that de-energises the wrong outputs the moment comms drop.

using AgOpenWeb.Services.RateControl;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class RcRelayMapTests
{
    private static RcRelay R(int id, RcRelayType t, int section = -1, int sw = 0) =>
        new() { Id = id, Type = t, SectionId = section, SwitchId = sw };

    private static RcRelayInputs Inputs(bool master = true, bool auto = false, bool moving = true,
        ulong sections = 0, bool calibrating = false, byte tram = 0, bool geoStop = false,
        byte hyd = 0, ushort switches = 0) => new()
    {
        MasterOn = master, AutoSectionOn = auto, Moving = moving, SectionBits = sections,
        Calibrating = calibrating, TramRight = (tram & 1) != 0, TramLeft = (tram & 2) != 0,
        GeoStop = geoStop, HydUp = hyd == 1, HydDown = hyd == 2, SwitchBits = switches,
    };

    [Test]
    public void DefaultMap_IsRelayNDrivesSectionN()
    {
        // The behaviour every existing setup was assuming before relay functions
        // existed — it has to survive the change untouched.
        var relays = RcRelayMap.DefaultRelays(moduleId: 0);
        var w = RcRelayMap.Compute(relays, Inputs(sections: 0b1010), RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b1010));
    }

    [Test]
    public void SecondModule_CountsSectionsOnFromSixteen()
    {
        var relays = RcRelayMap.DefaultRelays(moduleId: 1);
        Assert.That(relays[0].SectionId, Is.EqualTo(16));
        // Section 17 is module 1's relay 1.
        var w = RcRelayMap.Compute(relays, Inputs(sections: 1UL << 17), RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b10));
    }

    [Test]
    public void FlowMaster_FollowsMovementOnlyWhenAutoSectionsAreEngaged()
    {
        var relays = new[] { R(0, RcRelayType.FlowMaster) };

        // Manual: the operator's master switch is the whole story, stopped or not.
        var manual = RcRelayMap.Compute(relays, Inputs(master: true, auto: false, moving: false),
            RcFlowMasterMode.ThreeWire);
        Assert.That(manual.Relays, Is.EqualTo(1), "manual master on should keep flow on while stopped");

        // Auto: stopped at the headland means no product.
        var autoStopped = RcRelayMap.Compute(relays, Inputs(master: true, auto: true, moving: false),
            RcFlowMasterMode.ThreeWire);
        Assert.That(autoStopped.Relays, Is.Zero, "auto sections should shut flow off when stopped");

        var autoMoving = RcRelayMap.Compute(relays, Inputs(master: true, auto: true, moving: true),
            RcFlowMasterMode.ThreeWire);
        Assert.That(autoMoving.Relays, Is.EqualTo(1));
    }

    [Test]
    public void BypassIsTheOppositeOfAnySectionApplying()
    {
        var relays = new[] { R(0, RcRelayType.Bypass), R(1, RcRelayType.Slave) };

        var none = RcRelayMap.Compute(relays, Inputs(sections: 0), RcFlowMasterMode.ThreeWire);
        Assert.That(none.Relays, Is.EqualTo(0b01), "nothing applying: divert, slave off");

        var some = RcRelayMap.Compute(relays, Inputs(sections: 0b100), RcFlowMasterMode.ThreeWire);
        Assert.That(some.Relays, Is.EqualTo(0b10), "something applying: stop diverting, slave on");
    }

    [Test]
    public void InvertedWord_ListsTheRelaysThatRestEnergised()
    {
        // What the module holds on if it stops hearing from us. An inverted relay
        // is ON when the thing it mirrors is off, so leaving it de-energised would
        // be the wrong safe state.
        var relays = new[]
        {
            R(0, RcRelayType.Section, 0), R(1, RcRelayType.Bypass),
            R(2, RcRelayType.InvertMaster), R(3, RcRelayType.InvertFlowMaster),
            R(4, RcRelayType.InvertSection, 4), R(5, RcRelayType.Power),
        };
        var w = RcRelayMap.Compute(relays, Inputs(), RcFlowMasterMode.ThreeWire);
        Assert.Multiple(() =>
        {
            Assert.That(w.Inverted, Is.EqualTo(0b011110), "bypass + both inverts + invert-section");
            Assert.That(w.Power, Is.EqualTo(0b100000), "power relays are their own list");
        });
    }

    [Test]
    public void PowerRelayIsAlwaysOn_EvenWithMasterOff()
    {
        var relays = new[] { R(0, RcRelayType.Power), R(1, RcRelayType.Master) };
        var w = RcRelayMap.Compute(relays, Inputs(master: false), RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b01));
    }

    [Test]
    public void CalibrationForcesEverythingOn_BecauseTheMachineIsStandingStill()
    {
        // A catch test runs the meter with the machine parked and the master off,
        // so the usual master/speed gates would keep it shut.
        var relays = new[] { R(0, RcRelayType.Section, 0), R(1, RcRelayType.FlowMaster), R(2, RcRelayType.Master) };
        var w = RcRelayMap.Compute(relays,
            Inputs(master: false, auto: true, moving: false, sections: 0, calibrating: true),
            RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b111));
    }

    [Test]
    public void TramLiftAndGeoStopFollowTheMachine()
    {
        var relays = new[]
        {
            R(0, RcRelayType.TramRight), R(1, RcRelayType.TramLeft),
            R(2, RcRelayType.HydUp), R(3, RcRelayType.HydDown), R(4, RcRelayType.GeoStop),
        };
        // tram byte: bit 0 right, bit 1 left. hyd: 1 = raise.
        var w = RcRelayMap.Compute(relays, Inputs(tram: 0b10, hyd: 1, geoStop: true),
            RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b10110), "left tram, raise, geo stop");
    }

    [Test]
    public void SwitchRelayFollowsItsOwnSwitch()
    {
        var relays = new[] { R(0, RcRelayType.Switch, sw: 3), R(1, RcRelayType.Switch, sw: 5) };
        var w = RcRelayMap.Compute(relays, Inputs(switches: 1 << 5), RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b10));
    }

    [Test]
    public void TwoWireValveIndex_OnlyWithExactlyOneFlowMasterRelay()
    {
        var one = new[] { R(0, RcRelayType.Section, 0), R(3, RcRelayType.FlowMaster) };
        Assert.That(RcRelayMap.Compute(one, Inputs(), RcFlowMasterMode.TwoWire).FlowMasterIndex,
            Is.EqualTo(3), "the module needs to know which output to drive as a pair");

        // Two would leave the module guessing, so tell it nothing.
        var two = new[] { R(3, RcRelayType.FlowMaster), R(4, RcRelayType.FlowMaster) };
        Assert.That(RcRelayMap.Compute(two, Inputs(), RcFlowMasterMode.TwoWire).FlowMasterIndex,
            Is.EqualTo(RcRelayMap.NoFlowMasterValve));

        // The two-relay wiring drives open and close from a pair of assignments,
        // so there is no single index to send.
        Assert.That(RcRelayMap.Compute(one, Inputs(), RcFlowMasterMode.TwoWireInvertFlowMaster).FlowMasterIndex,
            Is.EqualTo(RcRelayMap.NoFlowMasterValve));
        Assert.That(RcRelayMap.Compute(one, Inputs(), RcFlowMasterMode.ThreeWire).FlowMasterIndex,
            Is.EqualTo(RcRelayMap.NoFlowMasterValve));
    }

    [Test]
    public void TwoRelayWiring_DrivesOpenAndCloseFromOppositeStates()
    {
        var relays = new[] { R(0, RcRelayType.FlowMaster), R(1, RcRelayType.InvertFlowMaster) };

        var on = RcRelayMap.Compute(relays, Inputs(master: true), RcFlowMasterMode.TwoWireInvertFlowMaster);
        Assert.That(on.Relays, Is.EqualTo(0b01), "open powered, close idle");

        var off = RcRelayMap.Compute(relays, Inputs(master: false), RcFlowMasterMode.TwoWireInvertFlowMaster);
        Assert.That(off.Relays, Is.EqualTo(0b10), "close powered, open idle");
    }

    [Test]
    public void NoneTypeRelayNeverSwitches()
    {
        var relays = new[] { R(0, RcRelayType.None, 0), R(1, RcRelayType.Section, 0) };
        var w = RcRelayMap.Compute(relays, Inputs(sections: 0b1), RcFlowMasterMode.ThreeWire);
        Assert.That(w.Relays, Is.EqualTo(0b10), "relay 0 is unassigned even though section 0 is on");
    }

    [Test]
    public void Renumber_RunsSectionsOnConsecutively_AndDropsWhatDoesNotFit()
    {
        var relays = new[]
        {
            R(0, RcRelayType.Section, 9), R(1, RcRelayType.Master), R(2, RcRelayType.Section, 4),
            R(3, RcRelayType.InvertSection, 7), R(4, RcRelayType.Section, 1),
        };
        RcRelayMap.Renumber(relays, startSection: 0, maxSections: 3);
        Assert.Multiple(() =>
        {
            Assert.That(relays[0].SectionId, Is.EqualTo(0));
            Assert.That(relays[1].Type, Is.EqualTo(RcRelayType.Master), "non-section relays are skipped, not renumbered");
            Assert.That(relays[2].SectionId, Is.EqualTo(1));
            Assert.That(relays[3].SectionId, Is.EqualTo(2));
            // Past the section count: switched off rather than left pointing at a
            // section that does not exist.
            Assert.That(relays[4].Type, Is.EqualTo(RcRelayType.None));
        });
    }

    [Test]
    public void EnsureRelays_FillsAnOldSetupWithTheDefaultItWasAssuming()
    {
        var m = new RcModuleSetup { ModuleId = 0 };   // saved before relay functions existed
        var relays = m.EnsureRelays();
        Assert.That(relays, Has.Count.EqualTo(16));
        Assert.That(relays[5].Type, Is.EqualTo(RcRelayType.Section));
        Assert.That(relays[5].SectionId, Is.EqualTo(5));
    }

    [Test]
    public void EnsureRelays_LeavesAssignedRelaysAlone()
    {
        var m = new RcModuleSetup { ModuleId = 0 };
        m.Relays.Add(R(0, RcRelayType.FlowMaster));
        var relays = m.EnsureRelays();
        Assert.That(relays, Has.Count.EqualTo(16));
        Assert.That(relays[0].Type, Is.EqualTo(RcRelayType.FlowMaster), "an existing assignment must survive");
        // Filled-in relays default to unassigned, not to a section — guessing a
        // section number for a board someone has already hand-wired would switch
        // outputs they never asked for.
        Assert.That(relays[1].Type, Is.EqualTo(RcRelayType.None));
    }
}
