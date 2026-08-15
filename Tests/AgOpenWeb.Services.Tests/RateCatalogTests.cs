// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// The product catalogue and its relationship to a tool's channels.
//
// The rule these pin down: a PRODUCT is shared (DAP goes through the fert
// spreader and the seed drill), but a CHANNEL's meter calibration belongs to
// one implement's flow meter and must never travel with the product.

using AgOpenWeb.Services.RateControl;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class RateCatalogTests
{
    [Test]
    public void AddOrUpdate_AddsThenUpdatesInPlace()
    {
        var c = new RateCatalog();
        var a = c.AddOrUpdate("DAP", "kg", 120);
        Assert.That(c.Items, Has.Count.EqualTo(1));
        Assert.That(a.Units, Is.EqualTo("kg"));

        var b = c.AddOrUpdate("DAP", "kg", 150);
        Assert.That(c.Items, Has.Count.EqualTo(1), "same product, not a duplicate");
        Assert.That(b.DefaultRate, Is.EqualTo(150));
        Assert.That(b, Is.SameAs(a));
    }

    [Test]
    public void Find_IsCaseInsensitive()
    {
        var c = new RateCatalog();
        c.AddOrUpdate("Urea 46", "kg", 70);
        Assert.That(c.Find("urea 46"), Is.Not.Null, "operators retype names casually");
        Assert.That(c.Find("nope"), Is.Null);
    }

    [Test]
    public void AddOrUpdate_KeepsExistingWhenBlanksPassed()
    {
        var c = new RateCatalog();
        c.AddOrUpdate("Roundup", "L", 2.5);
        c.AddOrUpdate("Roundup", "", 0);      // a partial edit must not wipe it
        var item = c.Find("Roundup")!;
        Assert.That(item.Units, Is.EqualTo("L"));
        Assert.That(item.DefaultRate, Is.EqualTo(2.5));
    }

    [Test]
    public void Remove_ReportsWhetherItExisted()
    {
        var c = new RateCatalog();
        c.AddOrUpdate("Lime", "t", 2);
        Assert.That(c.Remove("lime"), Is.True);
        Assert.That(c.Remove("lime"), Is.False);
        Assert.That(c.Items, Is.Empty);
    }

    [Test]
    public void SameProductOnTwoTools_KeepsSeparateCalibration()
    {
        // The whole point of the split: DAP in a spreader and in a drill is one
        // product, but each machine meters it with its own flow meter.
        var catalog = new RateCatalog();
        var dap = catalog.AddOrUpdate("DAP", "kg", 120);

        var spreaderChannel = new RateProduct { Name = dap.Name, Units = dap.Units, MeterCal = 180 };
        var drillChannel = new RateProduct { Name = dap.Name, Units = dap.Units, MeterCal = 742 };

        Assert.That(spreaderChannel.Name, Is.EqualTo(drillChannel.Name), "one shared product");
        Assert.That(spreaderChannel.MeterCal, Is.Not.EqualTo(drillChannel.MeterCal),
            "calibration belongs to the implement, not the product");

        // Re-picking the product must not carry a calibration across.
        drillChannel.Name = dap.Name;
        drillChannel.Units = dap.Units;
        drillChannel.TargetRate = dap.DefaultRate;
        Assert.That(drillChannel.MeterCal, Is.EqualTo(742));
    }
}
