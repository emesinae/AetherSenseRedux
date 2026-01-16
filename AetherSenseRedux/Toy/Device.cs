using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherSenseRedux.Pattern;
using Buttplug.Client;
using System.Threading;
using Dalamud.Utility;

namespace AetherSenseRedux.Toy
{
    internal class Device : IDisposable
    {
        public readonly ButtplugClientDevice ClientDevice;
        public List<IPattern> Patterns;
        public string Name => ClientDevice.Name;

        public double UPS =>
            // The actual UPS counter is derived from this internal average time per update value
            1000 / _ups;

        private double _ups = 16;       // we initialize this to the target time per update value just to avoid confusing users
        private double _lastIntensity;

        /// <summary>
        /// The time when we last sent a message to this device.
        /// </summary>
        private DateTime _lastWriteTime;
        private bool _active;
        private const int FrameTime = 16; // The target time per update, in this case 16ms = ~60 ups, and also a pipe dream for BLE toys.
        private readonly WaitType _waitType;
        private readonly Configuration configuration;

        public delegate void ErrorDelegate(Device device, Exception ex);
        public event ErrorDelegate? DeviceWriteError;

        public DeviceStatus Status =>
            new()
            {
                LastIntensity = _lastIntensity,
                UPS = UPS,
            };

        public Device(Configuration configuration, ButtplugClientDevice clientDevice, WaitType waitType)
        {
            this.configuration = configuration;
            ClientDevice = clientDevice;
            Patterns = new List<IPattern>();
            _lastIntensity = 0;
            _active = true;
            _waitType = waitType;
        }

        public void Dispose()
        {
            _active = false;
            Patterns.Clear();
            //ClientDevice.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            Task.Run(MainLoop).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task MainLoop()
        {
            Stopwatch timer = new();
            while (_active)
            {
                timer.Restart();
                await OnTick();
                var t = timer.ElapsedMilliseconds;
                if (t < FrameTime)
                {
                    switch (_waitType)
                    {
                        case WaitType.Use_Sleep:
                            // We use this instead of Task.Delay because Task.Delay seems to emulate the old 15.6ms
                            // system timers and often delays for up to three cycles longer than requested whereas
                            // modern Windows 10 systems have a system timer resolution of more like 1.56ms, which
                            // is much preferable since buttplug requires real-time messaging.
                            // Worst case it performs just as poorly as Task.Delay, but best case we get accuracy
                            // to every other millisecond. Which is less cpu intensive than a SpinWait but also more
                            // than good enough for butts.
                            while (timer.ElapsedMilliseconds < FrameTime)
                            {
                                Thread.Sleep(1);
                            }
                            break;
                        case WaitType.Use_Delay:
                            // The odds of us getting here are low, but it's better to plan for possible future changes
                            // to Task.Delay than to get caught off guard.
                            while (timer.ElapsedMilliseconds < FrameTime)
                            {
                                await Task.Delay(1);
                            }
                            break;
                        case WaitType.Slow_Timer:
                        default:
                            // if we're stuck with low resolution waits there's no point to spinning.
                            await Task.Delay(FrameTime - (int)timer.ElapsedMilliseconds);
                            break;
                    }

                }
                else
                {
                    Service.PluginLog.Verbose("OnTick for device {0} took {1}ms too long!", Name, t - FrameTime);
                }
                _ups = _ups * 0.9 + timer.ElapsedMilliseconds * 0.1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            _active = false;
            lock (Patterns)
            {
                Patterns.Clear();
            }

            Write(0).WaitSafely();
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task OnTick()
        {

            List<double> intensities = new();
            DateTime t = DateTime.UtcNow;
            var patternsToRemove = new List<IPattern>();
            lock (Patterns)
            {
                foreach (var pattern in Patterns)
                {
                    try
                    {
                        intensities.Add(pattern.GetIntensityAtTime(t));
                    }
                    catch (PatternExpiredException)
                    {
                        patternsToRemove.Add(pattern);
                    }
                }
            }
            foreach (var pattern in patternsToRemove)
            {
                lock (Patterns)
                {
                    Patterns.Remove(pattern);
                }
            }

            double intensity = 0;

            if (intensities.Any())
            {
                switch (configuration.Combiner)
                {
                    case Configuration.CombineMode.Average:
                        intensity = intensities.Average();
                        break;
                    case Configuration.CombineMode.Maximum:
                        intensity = intensities.Max();
                        break;
                    case Configuration.CombineMode.Add:
                        intensity = intensities.Sum();
                        break;
                    case Configuration.CombineMode.Asymptotic:
                        // TODO: Clamp component signals? Changes the behavior, though!
                        // At least all patterns for now are 0..1 theoretically anyway...
                        // Might be able to do something interesting with sub-zero patterns damping vibration.
                        intensity = 1.0 - intensities.Aggregate(1.0, (combined, signal) => combined * (1 - signal));
                        break;
                }
            }
            await Write(intensity).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="intensity"></param>
        private async Task Write(double intensity)
        {
            // clamp intensity before comparing to reduce unnecessary writes to device
            double clampedIntensity = Clamp(intensity, 0, 1);

            if (Math.Abs(_lastIntensity - clampedIntensity) < 0.00001)
            {
                return;
            }

            var lastWriteMs = (DateTime.Now - this._lastWriteTime).TotalMilliseconds;
            var timingCap = Math.Max(ClientDevice.MessageTimingGap, 30);
            if (lastWriteMs < timingCap)
            {
                return;
            }

            //Service.PluginLog.Info($"writing {clampedIntensity}: {lastWriteMs} >= {timingCap} - {ClientDevice.Name}");

            _lastIntensity = clampedIntensity;

            try
            {
                this._lastWriteTime = DateTime.Now;
                await ClientDevice.VibrateAsync(clampedIntensity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning($"Error while trying to send vibration value to device '{this.Name}': {ex.Message}.");
                // Connecting to an intiface server on Linux will spam the log with bluez errors
                // so we just ignore all exceptions from this statement. Good? Probably not. Necessary? Yes.
                this.DeviceWriteError?.Invoke(this, ex);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static double Clamp(double value, double min, double max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }
}
