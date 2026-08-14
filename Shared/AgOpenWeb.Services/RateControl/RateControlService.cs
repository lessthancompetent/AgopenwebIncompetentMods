// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Native rate control — Phase 1 of the AOG_RC (RateController) integration.
// Speaks the RC module plane directly (listen :29999, send broadcast :28888),
// so existing RC hardware (RCnano / RC teensy / ESP32 boards) works unchanged
// with no WinForms RateController app in the loop. Speed and section state come
// from this app's own pipeline — no AgIO PGN hop. Domain math ported from
// SK21/AOG_RC (GPL v3): see RcPgn.cs for the wire contract and RateProduct for
// the per-product model.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using AgOpenWeb.Models.Configuration;
using AgOpenWeb.Models.State;
using AgOpenWeb.Services.Interfaces;

namespace AgOpenWeb.Services.RateControl;

public interface IRateControlService
{
    IReadOnlyList<RateProduct> Products { get; }
    bool PlaneActive { get; }
    void Start();
    void Stop();
    void SetProductValue(int index, string key, string value);
    void ResetQuantity(int index);
    void ResetArea(int index);
    string BuildStatusJson();
}

public sealed class RateControlService : IRateControlService, IDisposable
{
    private const int ProductCount = 5;      // AOG_RC's fixed product slots
    private const int SendIntervalMs = 250;  // 4 Hz settings/target push (RC parity)

    private readonly ApplicationState _state;
    private readonly ConfigurationStore _configStore;
    private readonly ISectionControlService _sections;
    private readonly ISettingsService _settings;
    private readonly List<RateProduct> _products = new();
    private readonly object _ioLock = new();

    private Socket? _socket;
    private Timer? _sendTimer;
    private Thread? _rxThread;
    private volatile bool _running;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private DateTime _lastSaveUtc = DateTime.UtcNow;
    private bool _dirty;
    private List<IPEndPoint> _broadcastEndpoints = new();
    private DateTime _lastEndpointRefresh = DateTime.MinValue;
    /// <summary>Learned module addresses (moduleId → last frame source). Some RC
    /// module builds run the W5500 with broadcast blocking, so once a module is
    /// heard we also unicast to it directly.</summary>
    private readonly Dictionary<int, IPAddress> _moduleAddresses = new();

    public RateControlService(ApplicationState state, ConfigurationStore configStore,
        ISectionControlService sections, ISettingsService settings)
    {
        _state = state;
        _configStore = configStore;
        _sections = sections;
        _settings = settings;
    }

    public IReadOnlyList<RateProduct> Products => _products;

    public bool PlaneActive { get; private set; }

    private string ProductsFile
    {
        get
        {
            string root = Path.GetDirectoryName(_settings.Settings.FieldsDirectory)
                ?? _settings.Settings.FieldsDirectory;
            return Path.Combine(root, "RateController", "products.json");
        }
    }

    public void Start()
    {
        if (_running) return;
        LoadProducts();

        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            // ReuseAddress: lets the WinForms RC app run in parallel during the
            // transition (broadcast module frames reach both listeners).
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            if (OperatingSystem.IsWindows())
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                _socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            _socket.Bind(new IPEndPoint(IPAddress.Any, RcPgn.HostListenPort));
            PlaneActive = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Rate] module plane unavailable: {ex.Message}");
            _socket?.Dispose();
            _socket = null;
            PlaneActive = false;
        }

        _running = true;
        if (_socket != null)
        {
            _rxThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "RateControlRx" };
            _rxThread.Start();
            _sendTimer = new Timer(_ => SendTick(), null, SendIntervalMs, SendIntervalMs);
        }
    }

    public void Stop()
    {
        _running = false;
        _sendTimer?.Dispose();
        _sendTimer = null;
        _socket?.Close();
        _socket?.Dispose();
        _socket = null;
        PlaneActive = false;
        SaveProducts();
    }

    public void Dispose() => Stop();

    // ---- inbound: module frames -> product live state ----

    private void ReceiveLoop()
    {
        var buf = new byte[512];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (_running && _socket is { } sock)
        {
            try
            {
                int n = sock.ReceiveFrom(buf, ref from);
                if (n < 3) continue;
                int pgn = buf[1] << 8 | buf[0];
                if (pgn == RcPgn.PGN_SENSOR && RcPgn.TryParseSensor(buf.AsSpan(0, n), out var sf))
                {
                    if (from is IPEndPoint ip)
                        lock (_ioLock) _moduleAddresses[sf.ModuleId] = ip.Address;
                    foreach (var p in _products)
                        if (p.Enabled && p.ModuleId == sf.ModuleId && p.SensorId == sf.SensorId)
                        {
                            lock (_ioLock) p.ApplySensorFrame(sf);
                            _dirty = true;
                        }
                }
                else if (pgn == RcPgn.PGN_MODULE_STATUS && RcPgn.TryParseModuleStatus(buf.AsSpan(0, n), out var mf))
                {
                    if (from is IPEndPoint ip2)
                        lock (_ioLock) _moduleAddresses[mf.ModuleId] = ip2.Address;
                }
                // 32401 module status: liveness rides the sensor frames; nothing
                // further needed for Phase 1 (pressure/wifi diagnostics later).
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (!_running) break; }
            catch { }
        }
    }

    // ---- outbound: 4 Hz settings/target to each enabled product's module ----

    private void SendTick()
    {
        if (!_running || _socket == null) return;
        try
        {
            var (activeHaPerMin, totalHaPerMin) = HectaresPerMinute();

            // Area accrues with worked ground regardless of module presence
            // (like AOG_RC, coverage is app-side; quantity is module-measured).
            var now = DateTime.UtcNow;
            double dtMin = Math.Clamp((now - _lastTickUtc).TotalMinutes, 0, 0.1);
            _lastTickUtc = now;

            foreach (var p in _products)
            {
                if (!p.Enabled) continue;

                if (activeHaPerMin > 0)
                {
                    lock (_ioLock) p.AreaApplied += activeHaPerMin * dtMin;
                    _dirty = true;
                }

                double target = p.TargetUpm(activeHaPerMin, totalHaPerMin);
                if (target > 0 && p.MinUpm > 0 && target < p.MinUpm) target = p.MinUpm;

                bool reset;
                lock (_ioLock) { reset = p.ResetQuantityPending; p.ResetQuantityPending = false; }

                // No switchbox in Phase 1: master follows this app's section master
                // implicitly through the UPM (sections off -> target 0); the module
                // additionally wants MasterOn asserted (RC does the same when no
                // switchbox is connected).
                var frame = RcPgn.BuildRateSettings(
                    p.ModuleId, p.SensorId, target, p.MeterCal, p.ControlType,
                    masterOn: true, autoOn: p.AutoOn, resetQuantity: reset,
                    manualPwm: p.ManualPwm, productEnabled: p.Enabled);

                foreach (var ep in BroadcastEndpoints())
                    try { _socket.SendTo(frame, ep); } catch { }
                // Learned module address → also unicast (W5500 broadcast blocking).
                IPAddress? modAddr;
                lock (_ioLock) _moduleAddresses.TryGetValue(p.ModuleId, out modAddr);
                if (modAddr != null)
                    try { _socket.SendTo(frame, new IPEndPoint(modAddr, RcPgn.ModuleListenPort)); } catch { }
            }

            if (_dirty && (now - _lastSaveUtc).TotalSeconds > 10)
            {
                _dirty = false;
                _lastSaveUtc = now;
                SaveProducts();
            }
        }
        catch { /* keep the timer alive */ }
    }

    /// <summary>Per-interface directed broadcasts (x.x.x.255:28888) — a global
    /// 255.255.255.255 broadcast leaves only the default-route NIC on Windows,
    /// which is usually NOT the implement network. Mirrors the AOG plane's
    /// discovery behaviour. Refreshed every 30 s.</summary>
    private List<IPEndPoint> BroadcastEndpoints()
    {
        if ((DateTime.UtcNow - _lastEndpointRefresh).TotalSeconds < 30 && _broadcastEndpoints.Count > 0)
            return _broadcastEndpoints;
        var eps = new List<IPEndPoint>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    var ip = ua.Address.GetAddressBytes();
                    var mask = ua.IPv4Mask?.GetAddressBytes();
                    if (mask == null) continue;
                    var bc = new byte[4];
                    for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
                    eps.Add(new IPEndPoint(new IPAddress(bc), RcPgn.ModuleListenPort));
                }
            }
        }
        catch { }
        if (eps.Count == 0) eps.Add(new IPEndPoint(IPAddress.Broadcast, RcPgn.ModuleListenPort));
        _broadcastEndpoints = eps;
        _lastEndpointRefresh = DateTime.UtcNow;
        return eps;
    }

    /// <summary>Worked area rate: (active width × speed) and (total width × speed),
    /// in hectares/minute — ha/min = width_m × speed_kmh / 600 (AOG_RC formula).</summary>
    private (double Active, double Total) HectaresPerMinute()
    {
        double speedKmh = Math.Abs(_state.Vehicle.Speed) * 3.6;
        double activeW = 0, totalW = 0;
        var states = _sections.SectionStates;
        for (int i = 0; i < states.Count; i++)
        {
            double w = Math.Abs(states[i].PositionRight - states[i].PositionLeft);
            totalW += w;
            if (states[i].IsOn) activeW += w;
        }
        if (totalW <= 0)
        {
            totalW = _configStore.ActualToolWidth;
            activeW = 0;
        }
        return (activeW * speedKmh / 600.0, totalW * speedKmh / 600.0);
    }

    // ---- commands (from the web panel) ----

    public void SetProductValue(int index, string key, string value)
    {
        if (index < 0 || index >= _products.Count) return;
        var p = _products[index];
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        bool B() => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        double D() => double.TryParse(value, System.Globalization.NumberStyles.Float, inv, out var d) ? d : 0;

        lock (_ioLock)
        {
            switch (key)
            {
                case "name": p.Name = value.Trim(); break;
                case "enabled": p.Enabled = B(); break;
                case "moduleId": p.ModuleId = Math.Clamp((int)D(), 0, 15); break;
                case "sensorId": p.SensorId = Math.Clamp((int)D(), 0, 15); break;
                case "targetRate": p.TargetRate = Math.Max(0, D()); break;
                case "coverageUnits": p.CoverageUnits = Math.Clamp((int)D(), 0, 3); break;
                case "meterCal": p.MeterCal = Math.Max(0, D()); break;
                case "controlType": p.ControlType = (RcControlType)Math.Clamp((int)D(), 0, 5); break;
                case "constantUpm": p.ConstantUpm = B(); break;
                case "minUpm": p.MinUpm = Math.Max(0, D()); break;
                case "tankSize": p.TankSize = Math.Max(0, D()); break;
                case "tankRemaining": p.TankRemaining = Math.Max(0, D()); break;
                case "auto": p.AutoOn = B(); break;
                case "manualPwm": p.ManualPwm = Math.Clamp((int)D(), -255, 255); break;
            }
            _dirty = true;
        }
        SaveProducts();
    }

    public void ResetQuantity(int index)
    {
        if (index < 0 || index >= _products.Count) return;
        lock (_ioLock)
        {
            _products[index].QuantityApplied = 0;
            _products[index].ResetQuantityPending = true; // also zero the module counter
            _dirty = true;
        }
    }

    public void ResetArea(int index)
    {
        if (index < 0 || index >= _products.Count) return;
        lock (_ioLock) { _products[index].AreaApplied = 0; _dirty = true; }
    }

    // ---- status for the web panel ----

    public string BuildStatusJson()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder(1024);
        sb.Append("{\"plane\":").Append(PlaneActive ? "true" : "false").Append(",\"products\":[");
        lock (_ioLock)
        {
            for (int i = 0; i < _products.Count; i++)
            {
                var p = _products[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(JsonSerializer.Serialize(p.Name))
                  .Append(",\"enabled\":").Append(p.Enabled ? "true" : "false")
                  .Append(",\"moduleId\":").Append(p.ModuleId)
                  .Append(",\"sensorId\":").Append(p.SensorId)
                  .Append(",\"targetRate\":").Append(p.TargetRate.ToString("0.##", inv))
                  .Append(",\"coverageUnits\":").Append(p.CoverageUnits)
                  .Append(",\"meterCal\":").Append(p.MeterCal.ToString("0.###", inv))
                  .Append(",\"controlType\":").Append((int)p.ControlType)
                  .Append(",\"constantUpm\":").Append(p.ConstantUpm ? "true" : "false")
                  .Append(",\"auto\":").Append(p.AutoOn ? "true" : "false")
                  .Append(",\"manualPwm\":").Append(p.ManualPwm)
                  .Append(",\"connected\":").Append(p.ModuleConnected ? "true" : "false")
                  .Append(",\"upm\":").Append(p.MeasuredUpm.ToString("0.##", inv))
                  .Append(",\"pwm\":").Append(p.ModulePwm)
                  .Append(",\"hz\":").Append(p.Hz.ToString("0.#", inv))
                  .Append(",\"binEmpty\":").Append(p.BinEmpty ? "true" : "false")
                  .Append(",\"qty\":").Append(p.QuantityApplied.ToString("0.#", inv))
                  .Append(",\"area\":").Append(p.AreaApplied.ToString("0.##", inv))
                  .Append(",\"tankSize\":").Append(p.TankSize.ToString("0.#", inv))
                  .Append(",\"tank\":").Append(p.TankRemaining.ToString("0.#", inv))
                  .Append('}');
            }
        }
        sb.Append("]}");
        return sb.ToString();
    }

    // ---- persistence ----

    private void LoadProducts()
    {
        _products.Clear();
        try
        {
            if (File.Exists(ProductsFile))
            {
                var loaded = JsonSerializer.Deserialize<List<RateProduct>>(File.ReadAllText(ProductsFile));
                if (loaded != null) _products.AddRange(loaded);
            }
        }
        catch { }
        while (_products.Count < ProductCount)
            _products.Add(new RateProduct { Name = $"Product {(char)('A' + _products.Count)}" });
        if (_products.Count > ProductCount) _products.RemoveRange(ProductCount, _products.Count - ProductCount);
    }

    private void SaveProducts()
    {
        try
        {
            string dir = Path.GetDirectoryName(ProductsFile)!;
            Directory.CreateDirectory(dir);
            List<RateProduct> copy;
            lock (_ioLock) copy = new List<RateProduct>(_products);
            File.WriteAllText(ProductsFile, JsonSerializer.Serialize(copy,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Rate] save failed: {ex.Message}");
        }
    }
}
