// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using AgOpenWeb.Services.RateControl;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class RcSwitchboxTests
{
    [Test]
    public void SwitchFor_DefaultsToOwnSwitch_CappedAtEight()
    {
        var s = new RcSwitchboxSettings();   // no allocation stored
        Assert.Multiple(() =>
        {
            Assert.That(s.SwitchFor(0), Is.EqualTo(0));
            Assert.That(s.SwitchFor(7), Is.EqualTo(7));
            // sections past the eighth share the last switch rather than
            // pointing at switches the on-screen box does not show
            Assert.That(s.SwitchFor(11), Is.EqualTo(7));
        });
    }

    [Test]
    public void SwitchFor_UsesStoredAllocation_AndFillsPastItsEnd()
    {
        var s = new RcSwitchboxSettings { SectionSwitch = new[] { 2, 2, -1 } };
        Assert.Multiple(() =>
        {
            Assert.That(s.SwitchFor(0), Is.EqualTo(2), "grouped onto switch 3");
            Assert.That(s.SwitchFor(2), Is.EqualTo(-1), "explicitly unallocated");
            Assert.That(s.SwitchFor(3), Is.EqualTo(3), "past the stored array: default");
        });
    }
}
