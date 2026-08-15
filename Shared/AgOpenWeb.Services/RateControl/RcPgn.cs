// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Wire codecs for the AOG_RC (RateController) module plane — ported byte-exact
// from SK21/AOG_RC (RateAppSource/RateController/PGNs, GPL v3). RC modules
// (RCnano / RC teensy / ESP32 boards) speak these frames over UDP: the module
// listens on :28888 and reports to :29999. Framing differs from the AOG plane:
// the first two bytes are a little-endian 16-bit PGN id (no 0x80 0x81 header)
// and the CRC is the additive sum of ALL preceding bytes (from byte 0).

using System;

namespace AgOpenWeb.Services.RateControl;

/// <summary>Parsed PGN 32400 — sensor data from a rate module.</summary>
public readonly struct RcSensorFrame
{
    public int ModuleId { get; init; }
    public int SensorId { get; init; }
    /// <summary>Measured flow, units per minute.</summary>
    public double Upm { get; init; }
    /// <summary>Module's lifetime accumulated quantity (units).</summary>
    public double AccumulatedQuantity { get; init; }
    public int Pwm { get; init; }
    public bool SensorReceiving { get; init; }
    public bool BinEmpty { get; init; }
    public double Hz { get; init; }
}

/// <summary>Parsed PGN 32401 — module status.</summary>
public readonly struct RcModuleStatusFrame
{
    public int ModuleId { get; init; }
    public double Pressure { get; init; }
    public double WheelSpeed { get; init; }
    public int InoType { get; init; }
    public int InoId { get; init; }
    public bool WorkSwitch { get; init; }
    public bool EthernetConnected { get; init; }
    public bool GoodPinConfig { get; init; }
}

/// <summary>PGN 32502 payload — one sensor's valve/PID tuning, held in module EEPROM.
///
/// Fields are the ON-WIRE values, exactly as AOG_RC stores and displays them, so
/// numbers from its docs/forum threads transfer unchanged and there is no second
/// scaling layer to disagree with the firmware. What the module does with each is
/// noted per field. Defaults match AOG_RC's Props defaults.</summary>
public sealed class RcControlSettings
{
    /// <summary>Percent; module scales to 255 * v / 100.</summary>
    public byte MaxPwm { get; set; } = 100;
    /// <summary>Percent; module scales to 255 * v / 100.</summary>
    public byte MinPwm { get; set; } = 5;
    /// <summary>0-100; module divides by 100 (dimensionless, normalized PID).</summary>
    public byte Kp { get; set; } = 40;
    /// <summary>0-100; module divides by 100.</summary>
    public byte Ki { get; set; } = 60;
    /// <summary>Module divides by 1000 (i.e. percent x 10 on the wire).</summary>
    public byte Deadband { get; set; } = 20;
    /// <summary>Percent.</summary>
    public byte BrakePoint { get; set; } = 35;
    /// <summary>Percent.</summary>
    public byte PidSlowAdjust { get; set; } = 60;
    public byte SlewRate { get; set; } = 25;
    /// <summary>Module divides by 10.</summary>
    public byte MaxIntegral { get; set; } = 250;
    /// <summary>Module divides by 100.</summary>
    public byte TimedMinStart { get; set; } = 50;
    public ushort TimedAdjust { get; set; } = 80;
    public ushort TimedPause { get; set; } = 400;
    public byte PidTime { get; set; } = 150;
    /// <summary>Hz x 10. Module derives the longest accepted pulse: 10_000_000 / v microseconds.</summary>
    public byte PulseMinHz { get; set; } = 10;
    /// <summary>Hz. Module derives the shortest accepted pulse: 1_000_000 / v microseconds.</summary>
    public ushort PulseMaxHz { get; set; } = 1500;
    /// <summary>Flow averaging window in centiseconds; module clamps to 5-200 (50-2000 ms).</summary>
    public byte PulseSampleSize { get; set; } = 40;
}

/// <summary>PGN 32507 payload — which pins one sensor uses. Changing any of these
/// makes the module save and restart, so send the full set for a sensor at once.</summary>
public sealed class RcSensorPins
{
    public byte FlowPin { get; set; }
    /// <summary>Direction pin (IN1 on the driver).</summary>
    public byte DirPin { get; set; }
    /// <summary>PWM pin (IN2 on the driver).</summary>
    public byte PwmPin { get; set; }
    /// <summary>255 = no bin level sensor / no bin alarm.</summary>
    public byte BinPin { get; set; } = 255;
    public bool InvertBinSensor { get; set; }
}

/// <summary>PGN 32700 payload — module-wide config, including the only way to
/// assign a module its ID.</summary>
public sealed class RcModuleConfig
{
    public byte ModuleId { get; set; }
    public byte SensorCount { get; set; } = 1;
    public bool InvertRelayControl { get; set; }
    public bool InvertFlowControl { get; set; }
    public bool WorkPinMomentary { get; set; }
    public bool Is3WireValve { get; set; }
    public bool Ads1115Enabled { get; set; }
    /// <summary>Commissioning only: the module adopts <see cref="ModuleId"/> as its
    /// new ID regardless of what it currently is. Requires exactly ONE board on the
    /// network — every module listening will take the ID. Leave false for normal
    /// updates, where the ID acts as a filter instead.</summary>
    public bool AssignModuleId { get; set; }
    /// <summary>0 none, 1 GPIO, 2 PCA9555 x8, 3 PCA9555 x16, 4 MCP23017, 5 PCA9685, 6 PCF8574.</summary>
    public byte OnboardRelayType { get; set; }
    /// <summary>Same encoding as <see cref="OnboardRelayType"/>.</summary>
    public byte RemoteRelayType { get; set; }
    /// <summary>Flow/dir/PWM pins for sensor 0 and sensor 1 (6 bytes, in that order).</summary>
    public byte[] SensorPins { get; set; } = new byte[6];
    /// <summary>Relay output pins 0-15.</summary>
    public byte[] RelayPins { get; set; } = new byte[16];
    public byte WorkPin { get; set; }
    public byte PressurePin { get; set; }
}

/// <summary>Control-valve kind, mirrors AOG_RC's ControlTypeEnum ordering.</summary>
public enum RcControlType
{
    Valve = 0,
    ComboClose = 1,
    Motor = 2,
    MotorWeights = 3,
    Fan = 4,
    ComboCloseTimed = 5,
}

public static class RcPgn
{
    public const int ModuleListenPort = 28888;  // modules receive here
    public const int HostListenPort = 29999;    // RC app / this service receives here

    // Little-endian ids in bytes 0-1.
    public const ushort PGN_SENSOR = 32400;        // 0x90 0x7E module → host
    public const ushort PGN_MODULE_STATUS = 32401; // 0x91 0x7E module → host
    public const ushort PGN_RATE_SETTINGS = 32500; // 0xF4 0x7E host → module
    // Module SETUP packets. These carry settings the module keeps in EEPROM and
    // that nothing else can write: its own web page is only WiFi + a master
    // button. Without them a module cannot be commissioned or a valve retuned
    // away from a Windows machine running the RateController app.
    public const ushort PGN_CONTROL_SETTINGS = 32502; // 0xF6 0x7E valve/PID tuning
    public const ushort PGN_SENSOR_PINS = 32507;      // 0xFB 0x7E per-sensor pins
    public const ushort PGN_MODULE_CONFIG = 32700;    // 0xBC 0x7F module-wide config

    /// <summary>Additive CRC over bytes 0..length-1 (RC plane sums from byte 0,
    /// unlike the AOG plane which sums from byte 2).</summary>
    public static byte Crc(ReadOnlySpan<byte> data, int length)
    {
        int ck = 0;
        for (int i = 0; i < length; i++) ck += data[i];
        return (byte)ck;
    }

    public static bool GoodCrc(ReadOnlySpan<byte> data)
        => data.Length >= 3 && Crc(data, data.Length - 1) == data[^1];

    public static byte BuildModSenId(int moduleId, int sensorId)
        => (byte)((moduleId << 4) | (sensorId & 0x0F));

    public static int ParseModId(byte b) => (b >> 4) & 0x0F;

    public static int ParseSenId(byte b) => b & 0x0F;

    /// <summary>Parse PGN 32400 (sensor data). 15 bytes.</summary>
    public static bool TryParseSensor(ReadOnlySpan<byte> d, out RcSensorFrame frame)
    {
        frame = default;
        if (d.Length < 15 || d[0] != 0x90 || d[1] != 0x7E) return false;
        if (!GoodCrc(d[..15])) return false;

        frame = new RcSensorFrame
        {
            ModuleId = ParseModId(d[2]),
            SensorId = ParseSenId(d[2]),
            Upm = (d[5] << 16 | d[4] << 8 | d[3]) / 1000.0,
            AccumulatedQuantity = (d[8] << 16 | d[7] << 8 | d[6]) / 10.0,
            Pwm = (short)(d[10] << 8 | d[9]),
            SensorReceiving = (d[11] & 0x01) != 0,
            BinEmpty = (d[11] & 0x02) != 0,
            Hz = (d[12] | d[13] << 8) / 10.0,
        };
        return true;
    }

    /// <summary>Parse PGN 32401 (module status). 15 bytes.</summary>
    public static bool TryParseModuleStatus(ReadOnlySpan<byte> d, out RcModuleStatusFrame frame)
    {
        frame = default;
        if (d.Length < 15 || d[0] != 0x91 || d[1] != 0x7E) return false;
        if (!GoodCrc(d[..15])) return false;

        frame = new RcModuleStatusFrame
        {
            ModuleId = d[2],
            Pressure = (d[4] << 8 | d[3]) / 10.0,
            WheelSpeed = (d[6] << 8 | d[5]) / 10.0,
            InoType = d[10],
            InoId = d[12] << 8 | d[11],
            WorkSwitch = (d[13] & 0x01) != 0,
            EthernetConnected = (d[13] & 0x10) != 0,
            GoodPinConfig = (d[13] & 0x20) != 0,
        };
        return true;
    }

    public const ushort PGN_RELAY_SETTINGS = 32501; // 0xF5 0x7E host → module

    /// <summary>
    /// Build PGN 32501 (relay/section states host → module). 11 bytes.
    /// The firmware's auto-PID gate requires nonzero relay bits (PIDenabled =
    /// … && (RelayLo || RelayHi)), so this frame is what lets auto rate run at
    /// all; it also drives the module's physical relay outputs per section.
    /// <paramref name="masterValveIndex"/> 255 = no flow master valve.
    /// </summary>
    /// <summary>Build PGN 32502 (control settings) for one sensor. 24 bytes.
    /// The module stores these in EEPROM and runs the loop from them with the
    /// tablet off, so this is a push, not a live control message.</summary>
    public static byte[] BuildControlSettings(int moduleId, int sensorId, RcControlSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var d = new byte[24];
        d[0] = 0xF6;   // 246
        d[1] = 0x7E;   // 126
        d[2] = BuildModSenId(moduleId, sensorId);
        d[3] = s.MaxPwm;
        d[4] = s.MinPwm;
        d[5] = s.Kp;
        d[6] = s.Ki;
        d[7] = s.Deadband;
        d[8] = s.BrakePoint;
        d[9] = s.PidSlowAdjust;
        d[10] = s.SlewRate;
        d[11] = s.MaxIntegral;
        d[12] = 0;     // spare
        d[13] = s.TimedMinStart;
        d[14] = (byte)s.TimedAdjust;
        d[15] = (byte)(s.TimedAdjust >> 8);
        d[16] = (byte)s.TimedPause;
        d[17] = (byte)(s.TimedPause >> 8);
        d[18] = s.PidTime;
        d[19] = s.PulseMinHz;
        d[20] = (byte)s.PulseMaxHz;
        d[21] = (byte)(s.PulseMaxHz >> 8);
        d[22] = s.PulseSampleSize;
        d[23] = Crc(d, 23);
        return d;
    }

    /// <summary>Build PGN 32507 (sensor pins), one packet per sensor. 11 bytes.
    /// The module saves and schedules a restart when any pin actually changes.</summary>
    public static byte[] BuildSensorPins(int moduleId, int sensorId, RcSensorPins p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var d = new byte[11];
        d[0] = 0xFB;   // 251
        d[1] = 0x7E;   // 126
        d[2] = BuildModSenId(moduleId, sensorId);
        d[3] = p.FlowPin;
        d[4] = p.DirPin;
        d[5] = p.PwmPin;
        d[6] = p.BinPin;
        d[7] = (byte)(p.InvertBinSensor ? 1 : 0);
        d[8] = 0;      // spare
        d[9] = 0;      // spare
        d[10] = Crc(d, 10);
        return d;
    }

    /// <summary>Build PGN 32700 (module config). 33 bytes.
    ///
    /// With <see cref="RcModuleConfig.AssignModuleId"/> set the module adopts the
    /// ID unconditionally — the only way to commission a board — so exactly one
    /// board may be connected. Cleared, the ID is a filter and the packet is
    /// ignored by every other module.</summary>
    public static byte[] BuildModuleConfig(RcModuleConfig c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var d = new byte[33];
        d[0] = 0xBC;   // 188
        d[1] = 0x7F;   // 127
        d[2] = c.ModuleId;
        d[3] = c.SensorCount;
        byte cmd = 0;
        if (c.InvertRelayControl) cmd |= 1;
        if (c.InvertFlowControl) cmd |= 2;
        if (c.WorkPinMomentary) cmd |= 8;
        if (c.Is3WireValve) cmd |= 16;
        if (c.Ads1115Enabled) cmd |= 32;
        if (c.AssignModuleId) cmd |= 64;
        d[4] = cmd;
        d[5] = c.OnboardRelayType;
        d[6] = c.RemoteRelayType;
        var sp = c.SensorPins ?? Array.Empty<byte>();
        for (int i = 0; i < 6 && i < sp.Length; i++) d[7 + i] = sp[i];
        var rp = c.RelayPins ?? Array.Empty<byte>();
        for (int i = 0; i < 16 && i < rp.Length; i++) d[13 + i] = rp[i];
        d[29] = c.WorkPin;
        d[30] = c.PressurePin;
        d[31] = 0;     // spare
        d[32] = Crc(d, 32);
        return d;
    }

    public static byte[] BuildRelaySettings(int moduleId, byte relayLo, byte relayHi,
        byte powerRelayLo = 0, byte powerRelayHi = 0, byte invertedLo = 0, byte invertedHi = 0,
        byte masterValveIndex = 255)
    {
        var d = new byte[11];
        d[0] = 0xF5;   // 245
        d[1] = 0x7E;   // 126
        d[2] = BuildModSenId(moduleId, 0);
        d[3] = relayLo;
        d[4] = relayHi;
        d[5] = powerRelayLo;
        d[6] = powerRelayHi;
        d[7] = invertedLo;
        d[8] = invertedHi;
        d[9] = masterValveIndex;
        d[10] = Crc(d, 10);
        return d;
    }

    // PGN 32500 command bits (CommandPGN32500 in AOG_RC).
    private const byte CmdResetQuantity = 1;
    private const byte CmdMasterOn = 16;
    private const byte CmdAutoOn = 64;

    private static byte ControlTypeBits(RcControlType t) => t switch
    {
        RcControlType.ComboClose => 2,
        RcControlType.Motor => 4,
        RcControlType.MotorWeights => 6,
        RcControlType.Fan => 8,
        RcControlType.ComboCloseTimed => 10,
        _ => 0, // standard valve
    };

    /// <summary>
    /// Build PGN 32500 (rate settings host → module). 14 bytes.
    /// <paramref name="targetUpm"/> is the flow target in units/min the module's
    /// PID chases; <paramref name="meterCal"/> is pulses per unit (flow cal).
    /// </summary>
    public static byte[] BuildRateSettings(
        int moduleId, int sensorId, double targetUpm, double meterCal,
        RcControlType controlType, bool masterOn, bool autoOn, bool resetQuantity,
        int manualPwm, bool productEnabled)
    {
        var d = new byte[14];
        d[0] = 0xF4;   // 244
        d[1] = 0x7E;   // 126
        d[2] = BuildModSenId(moduleId, sensorId);

        if (productEnabled)
        {
            int rate = (int)(targetUpm * 1000.0);
            d[3] = (byte)rate;
            d[4] = (byte)(rate >> 8);
            d[5] = (byte)(rate >> 16);
        }

        int cal = (int)(meterCal * 1000.0);
        d[6] = (byte)cal;
        d[7] = (byte)(cal >> 8);
        d[8] = (byte)(cal >> 16);

        byte cmd = ControlTypeBits(controlType);
        if (resetQuantity) cmd |= CmdResetQuantity;
        if (masterOn) cmd |= CmdMasterOn;
        if (autoOn) cmd |= CmdAutoOn;
        d[9] = cmd;

        if (productEnabled)
        {
            d[10] = (byte)manualPwm;
            d[11] = (byte)(manualPwm >> 8);
        }

        d[13] = Crc(d, 13);
        return d;
    }
}
