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
