using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Buttplug.Client;
using AetherSenseRedux.Trigger;
using AetherSenseRedux.Pattern;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using AetherSenseRedux.Hooks;
using AetherSenseRedux.Toy;
using AetherSenseRedux.Trigger.Emote;
using Lumina.Excel.Sheets;

namespace AetherSenseRedux
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;

        private const string CommandName = "/asr";

        private Configuration Configuration { get; set; }

        private EmoteReaderHooks _emoteReaderHooks;

        private PluginUI PluginUi { get; init; }

        internal DeviceService DeviceService;

        private readonly List<ChatTrigger> _chatTriggerPool;
        private readonly List<EmoteTrigger> _emoteTriggerPool;

        /// <summary>
        /// 
        /// </summary>
        public Plugin()
        {
            PluginInterface.Inject(this);
            PluginInterface.Create<Service>();

            Service.Plugin = this;
            Service.PluginInterface = PluginInterface;

            this.DeviceService = new DeviceService();
            this.DeviceService.DeviceAdded += DeviceServiceOnDeviceAdded;
            this.DeviceService.Stopped += DeviceServiceOnStopped;
            this._chatTriggerPool = [];
            this._emoteTriggerPool = [];

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.FixDeserialization();

            // Update the configuration if it's an older version
            if (Configuration.Version == 1)
            {
                Configuration.Version = 2;
                Configuration.FirstRun = false;
                Configuration.Save();
            }

            if (Configuration.FirstRun)
            {
                Configuration.LoadDefaults();
            }

            PluginUi = new PluginUI(Configuration);

            Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnShowUI)
            {
                HelpMessage = "Opens the Aether Sense Redux configuration window"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            this._emoteReaderHooks = new EmoteReaderHooks();
            this._emoteReaderHooks.OnEmote += OnEmoteReceived;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            CleanTriggers();
            DeviceService.Dispose();
            PluginUi.Dispose();
            Service.CommandManager.RemoveHandler(CommandName);
            _emoteReaderHooks.Dispose();
        }

        // EVENT HANDLERS



        private void OnChatReceived(XivChatType type, int timestamp, ref SeString sender, ref SeString message,
            ref bool isHandled)
        {
            ChatMessage chatMessage = new(type, timestamp, ref sender, ref message, ref isHandled);
            foreach (var t in _chatTriggerPool)
            {
                t.Queue(chatMessage);
            }

            if (Configuration.LogChat)
            {
                Service.PluginLog.Debug(chatMessage.ToString());
            }
        }

        private void OnEmoteReceived(EmoteEvent e)
        {
            var isPerformer = e.Instigator.GameObjectId == Service.ClientState.LocalPlayer?.GameObjectId;
            var isTarget = e.Target?.GameObjectId == Service.ClientState.LocalPlayer?.GameObjectId;

            var emote = Service.DataManager.GetExcelSheet<Emote>().GetRowOrDefault(e.EmoteId);

            Service.PluginLog.Debug(
                $"{e.Instigator.Name} performed emote {emote?.Name.ExtractText() ?? "UNKNOWN"} ({e.EmoteId})" +
                (e.Target != null ? $" on target {e.Target.Name}" : string.Empty));

            var emoteLogItem = new EmoteLogItem
            {
                Instigator = e.Instigator,
                Target = e.Target,
                EmoteId = e.EmoteId,
                PlayerIsPerformer = isPerformer,
                PlayerIsTarget = isTarget,
                Timestamp = DateTime.Now,
            };

            foreach (var trigger in _emoteTriggerPool)
            {
                trigger.Queue(emoteLogItem);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        private void OnShowUI(string command, string args)
        {
            // in response to the slash command, just display our main ui
            PluginUi.SettingsVisible = true;
        }
        // END EVENT HANDLERS

        // SOME FUNCTIONS THAT DO THINGS
        /// <summary>
        /// 
        /// </summary>
        /// <param name="patternConfig">A pattern configuration.</param>
        public void DoPatternTest(dynamic patternConfig)
        {
            if (DeviceService.Status != ButtplugStatus.Connected)
            {
                return;
            }

            this.DeviceService.AddPatternTest(patternConfig);
        }

        // END SOME FUNCTIONS THAT DO THINGS

        // START AND STOP FUNCTIONS


        /// <summary>
        /// 
        /// </summary>
        private void CleanTriggers()
        {
            foreach (var t in _chatTriggerPool)
            {
                Service.PluginLog.Debug("Stopping chat trigger {0}", t.Name);
                t.Stop();
            }

            foreach (var t in _emoteTriggerPool)
            {
                Service.PluginLog.Debug("Stopping emote trigger {0}", t.Name);
                t.Stop();
            }

            Service.ChatGui.ChatMessage -= OnChatReceived;
            _chatTriggerPool.Clear();
            _emoteTriggerPool.Clear();
            Service.PluginLog.Debug("Triggers destroyed.");
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitTriggers()
        {
            foreach (var d in Configuration.Triggers)
            {
                // We pass DevicePool by reference so that triggers don't get stuck with outdated copies
                // of the device pool, should it be replaced with a new List<Device> - currently this doesn't
                // happen, but it's possible it may happen in the future.
                var trigger = TriggerFactory.GetTriggerFromConfig(d);
                if (trigger.Type == "ChatTrigger")
                {
                    _chatTriggerPool.Add((ChatTrigger)trigger);
                }
                else if (trigger.Type == "EmoteTrigger")
                {
                    _emoteTriggerPool.Add((EmoteTrigger)trigger);
                }
                else
                {
                    Service.PluginLog.Error("Invalid trigger type {0} created.", trigger.Type);
                }
            }

            foreach (var t in _chatTriggerPool)
            {
                Service.PluginLog.Debug("Starting chat trigger {0}", t.Name);
                t.Start();
            }

            foreach (var t in _emoteTriggerPool)
            {
                Service.PluginLog.Debug("Starting emote trigger {0}", t.Name);
                t.Start();
            }

            Service.ChatGui.ChatMessage += OnChatReceived;
            Service.PluginLog.Debug("Triggers created");
        }

        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            CleanTriggers();
            InitTriggers();
            DeviceService.Start(new Uri(Configuration.Address));
        }

        /// <summary>
        /// 
        /// </summary>
        public void Reload()
        {
            if (!DeviceService.Connected) return;
            CleanTriggers();
            InitTriggers();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Stop(bool expected)
        {
            DeviceService.Stop();
        }

        private void DeviceServiceOnDeviceAdded(Device device)
        {
            if (!Configuration.SeenDevices.Contains(device.Name))
            {
                Configuration.SeenDevices.Add(device.Name);
            }
        }

        private void DeviceServiceOnStopped(object? sender, EventArgs e)
        {
            CleanTriggers();
        }
        // END START AND STOP FUNCTIONS

        // UI FUNCTIONS
        /// <summary>
        /// 
        /// </summary>
        private void DrawUI()
        {
            this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.SettingsVisible = !PluginUi.SettingsVisible;
        }
        // END UI FUNCTIONS
    }
}
