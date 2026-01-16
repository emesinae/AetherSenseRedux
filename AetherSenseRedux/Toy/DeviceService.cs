using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AetherSenseRedux.Pattern;
using Buttplug.Client;
using Dalamud.Utility;

namespace AetherSenseRedux.Toy;

internal class DeviceService : IDisposable, IAsyncDisposable
{
    public delegate void DeviceDelegate(Device device);

    public event DeviceDelegate? DeviceAdded;
    public event EventHandler? Stopped;

    public WaitType WaitType { get; set; }

    private readonly List<Device> _devicePool;

    private ButtplugClient? _buttplug;
    private ButtplugWebsocketConnector? _buttplugWebsocketConnector;
    private ButtplugStatus _status;
    private CancellationTokenSource? _cancellationTokenSource = null;
    public bool Connected => _buttplug?.Connected ?? false;
    public Exception? LastException { get; set; }
    private readonly Configuration configuration;

    public DeviceService(Configuration configuration)
    {
        this.configuration = configuration;
        this._devicePool = [];
        _status = ButtplugStatus.Disconnected;

        var t = DoBenchmark();
        t.Wait();
        WaitType = t.Result;
    }

    public Dictionary<string, DeviceStatus> ConnectedDevices
    {
        get
        {
            Dictionary<string, DeviceStatus> result = new();
            foreach (var device in _devicePool)
            {
                result[device.Name] = device.Status;
            }

            return result;
        }
    }

    public ButtplugStatus Status
    {
        get
        {
            try
            {
                if (_buttplug == null)
                {
                    return ButtplugStatus.Uninitialized;
                }
                else if (_buttplug.Connected && _status == ButtplugStatus.Connected)
                {
                    return ButtplugStatus.Connected;
                }
                else if (_status == ButtplugStatus.Connecting)
                {
                    return ButtplugStatus.Connecting;
                }
                else if (!_buttplug.Connected && _status == ButtplugStatus.Connected)
                {
                    return ButtplugStatus.Error;
                }
                else if (_status == ButtplugStatus.Disconnecting)
                {
                    return ButtplugStatus.Disconnecting;
                }
                else if (LastException != null)
                {
                    return ButtplugStatus.Error;
                }
                else
                {
                    return ButtplugStatus.Disconnected;
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "error when getting status");
                return ButtplugStatus.Error;
            }


        }
    }

    public ReadOnlyCollection<Device> Devices => new ReadOnlyCollection<Device>(_devicePool);

    public void Start(Uri address)
    {
        Task.Run(() => InitButtplug(address));
    }

    /// <summary>
    /// 
    /// </summary>
    private async Task InitButtplug(Uri address)
    {
        LastException = null;
        _status = ButtplugStatus.Connecting;

        if (_buttplug == null)
        {
            _buttplug = new ButtplugClient("AetherSense Redux");
            _buttplug.DeviceAdded += OnDeviceAdded;
            _buttplug.DeviceRemoved += OnDeviceRemoved;
            _buttplug.ScanningFinished += OnScanComplete;
            _buttplug.ServerDisconnect += OnServerDisconnect;
        }

        if (!Connected)
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _buttplugWebsocketConnector = new ButtplugWebsocketConnector(address);
                await _buttplug.ConnectAsync(_buttplugWebsocketConnector, _cancellationTokenSource.Token);
                await DoScan();
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Buttplug failed to connect.");
                LastException = ex;
                Stop();
            }
        }

        if (Connected)
        {
            Service.PluginLog.Information("Buttplug connected.");
            _status = ButtplugStatus.Connected;
        }

    }

    private void RemoveClientDevice(ButtplugClientDevice clientDevice)
    {
        var toRemove = new List<Device>();
        lock (this._devicePool)
        {
            foreach (var device in this._devicePool.Where(device => device.ClientDevice == clientDevice))
            {
                try
                {
                    device.Stop();
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Could not stop device {0}, device disconnected?", device.Name);
                }
                toRemove.Add(device);
                device.Dispose();
            }
        }
        foreach (var device in toRemove)
        {
            lock (this._devicePool)
            {
                this._devicePool.Remove(device);
            }

        }
    }

    public void AddPatternTest(dynamic patternConfig)
    {
        lock (_devicePool)
        {
            foreach (var device in this._devicePool)
            {
                lock (device.Patterns)
                {
                    device.Patterns.Add(PatternFactory.GetPatternFromObject(patternConfig));
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>The task associated with this method.</returns>
    private async Task DoScan()
    {
        try
        {
            await _buttplug!.StartScanningAsync();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Asynchronous scanning failed.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private async Task DisconnectButtplugAsync()
    {
        if (_buttplug?.Connected == true)
        {
            try
            {
                if (_cancellationTokenSource != null)
                {
                    await _cancellationTokenSource.CancelAsync();
                }

                if (_buttplugWebsocketConnector?.Connected ?? false)
                {
                    Service.PluginLog.Debug("Websocket is still connected. Disconnecting...");
                    await _buttplugWebsocketConnector.DisconnectAsync();
                    _buttplugWebsocketConnector = null;
                    Service.PluginLog.Debug("Websocket disconnected");
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && ex.InnerException is ObjectDisposedException)
                {
                    Service.PluginLog.Warning(ex, "Buttplug websocket appears to have already disconnected");
                    _buttplugWebsocketConnector = null;
                }
                else
                {
                    Service.PluginLog.Error(ex, "Failed to disconnect Buttplug websocket.");
                }
            }

            try
            {
                await _buttplug!.DisconnectAsync();
                Service.PluginLog.Information("Buttplug disconnected.");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to disconnect Buttplug.");
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private async Task CleanButtplugAsync()
    {
        if (_buttplug == null)
        {
            _status = ButtplugStatus.Disconnected;
            return;
        }

        try
        {
            _status = ButtplugStatus.Disconnecting;
            await DisconnectButtplugAsync();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error thrown while trying to disconnect: {ex.Message}");
        }
        finally
        {
            _buttplug = null;
            _status = ButtplugStatus.Disconnected;
        }

        Service.PluginLog.Debug("Buttplug destroyed.");
    }

    private void CleanButtplug()
    {
        var cleanTask = CleanButtplugAsync();
        try
        {
            // we can't use WaitSafely inside async operations
            // so for (hacky) ease, we'll try it,
            // and then use Wait() if this throws.
            cleanTask.WaitSafely();
        }
        catch (InvalidOperationException)
        {
            cleanTask.Wait();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void CleanDevices()
    {
        lock (_devicePool)
        {
            foreach (var device in _devicePool)
            {
                Service.PluginLog.Debug("Stopping device {0}", device.Name);
                device.Stop();
                device.Dispose();
            }
            _devicePool.Clear();
        }
        Service.PluginLog.Debug("Devices destroyed.");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDeviceAdded(object? sender, DeviceAddedEventArgs e)
    {

        Service.PluginLog.Information("Device {0} added", e.Device.Name);
        Device newDevice = new(this.configuration, e.Device, WaitType);
        newDevice.DeviceWriteError += OnDeviceWriteError;
        lock (this._devicePool)
        {
            this._devicePool.Add(newDevice);
        }
        this.DeviceAdded?.Invoke(newDevice);

        newDevice.Start();
    }

    private void OnDeviceWriteError(Device device, Exception ex)
    {
        Service.PluginLog.Warning(ex, $"Device {device.Name} write error: {ex.Message}");
        this.RemoveClientDevice(device.ClientDevice);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDeviceRemoved(object? sender, DeviceRemovedEventArgs e)
    {
        if (Status != ButtplugStatus.Connected)
        {
            return;
        }
        Service.PluginLog.Information("Device {0} removed", e.Device.Name);
        this.RemoveClientDevice(e.Device);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnScanComplete(object? sender, EventArgs e)
    {
        // Do nothing, since Buttplug still keeps scanning for BLE devices even after scanning is "complete"
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnServerDisconnect(object? sender, EventArgs e)
    {
        if (Status == ButtplugStatus.Disconnecting)
        {
            return;
        }

        Stop();
        Service.PluginLog.Error("Unexpected disconnect.");
    }

    public void Stop()
    {
        this.Stopped?.Invoke(this, EventArgs.Empty);
        CleanDevices();
        CleanButtplug();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private static async Task<WaitType> DoBenchmark()
    {
        var result = WaitType.Slow_Timer;
        var times = new long[10];
        long sum = 0;
        var averages = new double[2];
        Stopwatch timer = new();
        Service.PluginLog.Information("Starting benchmark");


        Service.PluginLog.Debug("Testing Task.Delay");

        for (var i = 0; i < times.Length; i++)
        {
            timer.Restart();
            await Task.Delay(1);
            times[i] = timer.Elapsed.Ticks;
        }
        foreach (var t in times)
        {
            Service.PluginLog.Debug("{0}", t);
            sum += t;
        }
        averages[0] = (double)sum / times.Length / 10000;
        Service.PluginLog.Debug("Average: {0}", averages[0]);

        Service.PluginLog.Debug("Testing Thread.Sleep");
        times = new long[10];
        for (var i = 0; i < times.Length; i++)
        {
            timer.Restart();
            Thread.Sleep(1);
            times[i] = timer.Elapsed.Ticks;
        }
        sum = 0;
        foreach (var t in times)
        {
            Service.PluginLog.Debug("{0}", t);
            sum += t;
        }
        averages[1] = (double)sum / times.Length / 10000;
        Service.PluginLog.Debug("Average: {0}", averages[1]);

        if (averages[0] < 3)
        {
            result = WaitType.Use_Delay;

        }
        else if (averages[1] < 3)
        {
            result = WaitType.Use_Sleep;

        }

        switch (result)
        {
            case WaitType.Use_Delay:
                Service.PluginLog.Information("High resolution Task.Delay found, using delay in timing loops.");
                break;
            case WaitType.Use_Sleep:
                Service.PluginLog.Information("High resolution Thread.Sleep found, using sleep in timing loops.");
                break;
            case WaitType.Slow_Timer:
            default:
                Service.PluginLog.Information("No high resolution, CPU-friendly waits available, timing loops will be inaccurate.");
                break;
        }

        return result;

    }

    public void Dispose()
    {
        CleanDevices();
        CleanButtplug();
    }

    public async ValueTask DisposeAsync()
    {
        CleanDevices();
        await CleanButtplugAsync();
    }
}