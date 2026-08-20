// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using AgOpenWeb.Services.Interfaces;
using Newtonsoft.Json;

namespace AgOpenWeb.Services;

/// <summary>
/// USB/serial module connection — the AgIO serial role, in-app. A module wired
/// over USB instead of Ethernet speaks the same PGN byte stream; this bridge
/// frames the serial stream (binary PGNs + NMEA lines) and injects each frame
/// into the UDP service's normal receive path, and mirrors every module-bound
/// PGN the app sends back out the port. To the rest of the app a serial module
/// is indistinguishable from a network one (its "address" reads "serial").
/// </summary>
public class SerialBridgeService : IDisposable
{
    private readonly IUdpCommunicationService _udp;
    private readonly ISettingsService _settings;
    private readonly object _lock = new();

    private SerialPort? _port;
    private Thread? _readThread;
    private volatile bool _running;
    private long _rxFrames, _txFrames;
    private string? _lastError;

    public class Config
    {
        public string Port { get; set; } = "";
        public int Baud { get; set; } = 38400;
        public bool Enabled { get; set; }
    }

    private Config _cfg = new();

    public SerialBridgeService(IUdpCommunicationService udp, ISettingsService settings)
    {
        _udp = udp;
        _settings = settings;
    }

    private string ConfigPath
    {
        get
        {
            string root = Path.GetDirectoryName(_settings.Settings.FieldsDirectory)
                ?? _settings.Settings.FieldsDirectory;
            return Path.Combine(root, "serial-bridge.json");
        }
    }

    public void Start()
    {
        try
        {
            if (File.Exists(ConfigPath))
                _cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath)) ?? new Config();
        }
        catch { _cfg = new Config(); }
        if (_cfg.Enabled && !string.IsNullOrEmpty(_cfg.Port)) Open();
    }

    public void SetConfig(string port, int baud)
    {
        lock (_lock)
        {
            _cfg.Port = port;
            _cfg.Baud = Math.Clamp(baud, 4800, 921600);
            Persist();
            if (_cfg.Enabled) { Close(); Open(); }
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_lock)
        {
            _cfg.Enabled = enabled;
            Persist();
            if (enabled) Open(); else Close();
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_cfg, Formatting.Indented));
        }
        catch { }
    }

    private void Open()
    {
        Close();
        if (string.IsNullOrEmpty(_cfg.Port)) { _lastError = "no port selected"; return; }
        try
        {
            _port = new SerialPort(_cfg.Port, _cfg.Baud) { ReadTimeout = 250, WriteTimeout = 250 };
            _port.Open();
            _running = true;
            _lastError = null;
            _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "SerialBridge" };
            _readThread.Start();
            _udp.SerialMirror = TxFrame;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _port?.Dispose();
            _port = null;
        }
    }

    private void Close()
    {
        _running = false;
        _udp.SerialMirror = null;
        try { _port?.Close(); } catch { }
        try { _readThread?.Join(600); } catch { }
        _port?.Dispose();
        _port = null;
        _readThread = null;
    }

    /// <summary>App → module: raw PGN frame out the wire, as-is.</summary>
    private void TxFrame(byte[] frame)
    {
        var p = _port;
        if (p is not { IsOpen: true }) return;
        try { p.Write(frame, 0, frame.Length); _txFrames++; }
        catch (Exception ex) { _lastError = ex.Message; }
    }

    /// <summary>Module → app: sync on the PGN header (0x80 0x81 src pgn len …
    /// crc, total len+6) or a '$'…newline NMEA sentence, then inject the
    /// complete frame into the normal receive path.</summary>
    private void ReadLoop()
    {
        var buf = new byte[512];
        int have = 0;
        while (_running)
        {
            var p = _port;
            if (p is not { IsOpen: true }) break;
            int n;
            try { n = p.Read(buf, have, buf.Length - have); }
            catch (TimeoutException) { continue; }
            catch { break; }
            if (n <= 0) continue;
            have += n;

            int used = 0;
            while (true)
            {
                // Skip garbage to the next plausible frame start.
                while (used < have && buf[used] != 0x80 && buf[used] != (byte)'$') used++;

                if (used >= have) break;
                int avail = have - used;

                if (buf[used] == (byte)'$')
                {
                    int nl = Array.IndexOf(buf, (byte)'\n', used, avail);
                    if (nl < 0) { if (avail > 256) used++; break; }
                    int len = nl - used + 1;
                    var frame = new byte[len];
                    Array.Copy(buf, used, frame, 0, len);
                    _udp.InjectSerialFrame(frame);
                    _rxFrames++;
                    used = nl + 1;
                    continue;
                }

                // Binary: need header+len byte before sizing the frame.
                if (avail < 5) break;
                if (buf[used + 1] != 0x81) { used++; continue; }
                int total = buf[used + 4] + 6;
                if (total > 260) { used++; continue; }
                if (avail < total) break;
                var pgn = new byte[total];
                Array.Copy(buf, used, pgn, 0, total);
                _udp.InjectSerialFrame(pgn);
                _rxFrames++;
                used += total;
            }

            if (used > 0)
            {
                Array.Copy(buf, used, buf, 0, have - used);
                have -= used;
            }
            else if (have == buf.Length)
            {
                have = 0; // pathological garbage fill — start over
            }
        }
    }

    public string BuildStatusJson()
    {
        string[] ports;
        try { ports = SerialPort.GetPortNames(); } catch { ports = Array.Empty<string>(); }
        var p = _port;
        return JsonConvert.SerializeObject(new
        {
            enabled = _cfg.Enabled,
            port = _cfg.Port,
            baud = _cfg.Baud,
            open = p is { IsOpen: true },
            rxFrames = _rxFrames,
            txFrames = _txFrames,
            error = _lastError,
            ports,
        });
    }

    public void Dispose() => Close();
}
