// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// The product catalogue: what a product IS, independent of any machine.
//
// Products are many-to-many with both tools and jobs — DAP goes through the fert
// spreader and the seed drill; two sprayers apply the same chemical — so a
// product cannot be a field on either. It is its own thing, referenced by name:
//
//   catalogue (here, global)   what the product is: name, units, usual rate
//   channel   (per tool)       the machine's plumbing: module/sensor, meter
//                              calibration, tank size, valve type
//   job record (per job)       what actually went on: product, rate, quantity
//
// The split matters most for meter calibration: it belongs to the flow meter on
// one implement, and must never follow a product onto a different machine.

using System;
using System.Collections.Generic;

namespace AgOpenWeb.Services.RateControl;

/// <summary>One product in the catalogue. Shared by every tool and job.</summary>
public sealed class RateCatalogItem
{
    /// <summary>Display name, and the key channels and job records reference.</summary>
    public string Name { get; set; } = "";
    /// <summary>Dispensed units — L, kg, seeds. The rate unit is this per hectare.</summary>
    public string Units { get; set; } = "L";
    /// <summary>Usual application rate, applied when the product is put in a
    /// channel. The channel (and the job) can then differ from it.</summary>
    public double DefaultRate { get; set; } = 100;
}

/// <summary>Global list of products. Plain list rather than a keyed store: it is
/// short, hand-editable, and referenced by name from channels and job records.</summary>
public sealed class RateCatalog
{
    public List<RateCatalogItem> Items { get; set; } = new();

    public RateCatalogItem? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var i in Items)
            if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)) return i;
        return null;
    }

    /// <summary>Add or update by name; returns the stored item.</summary>
    public RateCatalogItem AddOrUpdate(string name, string units, double defaultRate)
    {
        var existing = Find(name);
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(units)) existing.Units = units;
            if (defaultRate > 0) existing.DefaultRate = defaultRate;
            return existing;
        }
        var item = new RateCatalogItem
        {
            Name = (name ?? "").Trim(),
            Units = string.IsNullOrWhiteSpace(units) ? "L" : units,
            DefaultRate = defaultRate > 0 ? defaultRate : 100,
        };
        Items.Add(item);
        return item;
    }

    public bool Remove(string name)
    {
        var item = Find(name);
        return item != null && Items.Remove(item);
    }
}
