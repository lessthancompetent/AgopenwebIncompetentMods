// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Saved copy of what a rate module holds in its own EEPROM: pin assignments,
// module-wide flags and per-sensor valve tuning (PGNs 32700 / 32507 / 32502).
//
// Why keep a copy at all — the module already stores these? Because nothing can
// read them back. There is no "report your config" packet (only PID diagnostics
// and a board label), so if a module is reset, swapped or reflashed, whatever
// was tuned into it is simply gone. Holding the values here makes the TOOL the
// master record of its implement's rate setup: load the tool, push, and the
// module is commissioned again — no Windows machine with the RateController app
// involved, which matters because there no longer is one in the tractor.
//
// Consequence: the first time a module is set up, its existing values have to be
// typed in to match. From then on this file is authoritative.

using System.Collections.Generic;

namespace AgOpenWeb.Services.RateControl;

/// <summary>One sensor (product channel) on a module: its pins and valve tuning.</summary>
public sealed class RcSensorSetup
{
    public int SensorId { get; set; }
    public RcSensorPins Pins { get; set; } = new();
    public RcControlSettings Control { get; set; } = new();
}

/// <summary>One rate module and its sensors.</summary>
public sealed class RcModuleSetup
{
    public int ModuleId { get; set; }
    public RcModuleConfig Config { get; set; } = new();
    public List<RcSensorSetup> Sensors { get; set; } = new();

    public RcSensorSetup GetOrAddSensor(int sensorId)
    {
        foreach (var s in Sensors)
            if (s.SensorId == sensorId) return s;
        var added = new RcSensorSetup { SensorId = sensorId };
        Sensors.Add(added);
        return added;
    }
}
