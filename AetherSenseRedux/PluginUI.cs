using AetherSenseRedux.Pattern;
using AetherSenseRedux.Trigger;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherSenseRedux.Trigger.Emote;
using AetherSenseRedux.UI;
using AetherSenseRedux.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using XIVChatTypes;
using Dalamud.Bindings.ImPlot;

namespace AetherSenseRedux
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration _configuration;

        private bool _settingsVisible = false;
        public bool SettingsVisible
        {
            get => _settingsVisible;
            set => _settingsVisible = value;
        }

        private int _selectedTrigger = 0;
        private int _selectedFilterCategory = 0;

        private readonly EmoteSelectionModal _emoteSelectionModal;

        // In order to keep the UI from trampling all over the configuration as changes are being made, we keep a working copy here when needed.
        private Configuration? _workingCopy;

        public PluginUI(Configuration configuration)
        {
            this._configuration = configuration;
            this._emoteSelectionModal = new EmoteSelectionModal("SelectEmotePopup");
        }

        /// <summary>
        /// Would dispose of any unmanaged resources if we used any here. Since we don't, we probably don't need this.
        /// </summary>
        public void Dispose()
        {

        }

        /// <summary>
        /// Draw handler for plugin UI
        /// </summary>
        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawSettingsWindow();
        }

        /// <summary>
        /// Draws the settings window and does a little housekeeping with the working copy of the config. 
        /// </summary>
        private void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {

                // if we aren't drawing the window we don't need a working copy of the configuration
                if (_workingCopy == null) return;
                Service.PluginLog.Debug("Making WorkingCopy null.");
                _workingCopy = null;

                return;
            }

            // we can only get here if we know we're going to draw the settings window, so let's get our working copy back

            if (_workingCopy == null)
            {
                Service.PluginLog.Debug("WorkingCopy was null, importing current config.");
                _workingCopy = new Configuration();
                _workingCopy.Import(_configuration);
            }

            ////
            ////    SETTINGS WINDOW
            ////
            ImGui.SetNextWindowSize(new Vector2(640, 500), ImGuiCond.Appearing);
            if (ImGui.Begin("AetherSense Redux", ref _settingsVisible, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar))
            {

                ////
                ////    MENU BAR
                ////
                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.BeginMenu("File"))
                    {
                        ImGui.MenuItem("Import...", "", false, false);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("NOT IMPLEMENTED");
                        }
                        ImGui.MenuItem("Export...", "", false, false);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("NOT IMPLEMENTED");
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenuBar();
                }

                ////
                ////    BODY
                ////
                ImGui.BeginChild("body", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), false);

                ImGui.Indent(1); //for some reason the UI likes to cut off a pixel on the left side if we don't do this

                if (ImGui.BeginTabBar("MyTabBar", ImGuiTabBarFlags.None))
                {
                    if (ImGui.BeginTabItem("Intiface"))
                    {
                        var address = _workingCopy.Address;
                        if (ImGui.InputText("Intiface Address", ref address, 64))
                        {
                            _workingCopy.Address = address;
                        }
                        ImGui.SameLine();
                        if (Service.Plugin.DeviceService.Status == ButtplugStatus.Connected)
                        {
                            if (ImGui.Button("Disconnect"))
                            {
                                Service.Plugin.Stop(true);
                            }
                        }
                        else if (Service.Plugin.DeviceService.Status == ButtplugStatus.Connecting || Service.Plugin.DeviceService.Status == ButtplugStatus.Disconnecting)
                        {
                            if (ImGui.Button("Wait..."))
                            {

                            }
                        }
                        else
                        {
                            if (ImGui.Button("Connect"))
                            {
                                _configuration.Address = _workingCopy.Address;
                                Service.Plugin.Start();
                            }
                        }

                        ImGui.Spacing();
                        ImGui.BeginChild("status", new Vector2(0, 0), true);
                        if (Service.Plugin.DeviceService.WaitType == WaitType.Slow_Timer)
                        {
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), "High resolution timers not available, patterns will be inaccurate.");
                        }
                        ImGui.Text("Connection Status:");
                        ImGui.Indent();
                        ImGui.Text(Service.Plugin.DeviceService.Status == ButtplugStatus.Connected ? "Connected" : Service.Plugin.DeviceService.Status == ButtplugStatus.Connecting ? "Connecting..." : Service.Plugin.DeviceService.Status == ButtplugStatus.Error ? "Error" : "Disconnected");
                        if (Service.Plugin.DeviceService.LastException != null)
                        {
                            ImGui.Text(Service.Plugin.DeviceService.LastException.Message);
                        }
                        ImGui.Unindent();
                        if (Service.Plugin.DeviceService.Status == ButtplugStatus.Connected)
                        {
                            ImGui.Text("Devices Connected:");
                            ImGui.Indent();
                            foreach (var device in Service.Plugin.DeviceService.ConnectedDevices)
                            {
                                ImGui.Text($"{device.Key} - {(int)(device.Value.LastIntensity * 100)}% [{(int)device.Value.UPS}]");
                            }
                            ImGui.Unindent();
                        }

                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Triggers"))
                    {
                        ImGui.BeginChild("leftouter", new Vector2(155, 0));
                        ImGui.Indent(1);
                        ImGui.BeginChild("left", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true);

                        foreach (var (t, i) in _workingCopy.Triggers.Select((value, i) => (value, i)))
                        {
                            ImGui.PushID(i); // We push the iterator to the ID stack so multiple triggers of the same type and name are still distinct
                            var label = $"{t.Name} ({t.Type})";
                            if (ImGui.Selectable(label, _selectedTrigger == i))
                            {
                                _selectedTrigger = i;
                            }
                            ImGui.PopID();
                        }

                        ImGui.EndChild();
                        if (ImGui.Button("Add New"))
                            ImGui.OpenPopup("SelectTriggerTypePopup");

                        if (ImGui.BeginPopup("SelectTriggerTypePopup"))
                        {
                            var triggers = _workingCopy.Triggers;

                            if (ImGui.Selectable("Chat Trigger"))
                            {
                                triggers.Add(new ChatTriggerConfig()
                                {
                                    PatternSettings = new ConstantPatternConfig()
                                });
                            }

                            if (ImGui.Selectable("Emote Trigger"))
                            {
                                triggers.Add(new EmoteTriggerConfig()
                                {
                                    PatternSettings = new ConstantPatternConfig()
                                });
                            }

                            ImGui.EndPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            _workingCopy.Triggers.RemoveAt(_selectedTrigger);
                            if (_selectedTrigger >= _workingCopy.Triggers.Count)
                            {
                                _selectedTrigger = (_selectedTrigger > 0) ? _workingCopy.Triggers.Count - 1 : 0;
                            }
                        }

                        ImGui.EndChild();
                        ImGui.SameLine();

                        ImGui.BeginChild("right", new Vector2(0, 0), false);
                        ImGui.Indent(1);
                        if (_workingCopy.Triggers.Count == 0 || _selectedTrigger < 0 || _selectedTrigger >= _workingCopy.Triggers.Count)
                        {
                            ImGui.Text("Use the Add New button to add a trigger.");
                        }
                        else
                        {
                            DrawTriggerConfig(_workingCopy.Triggers[_selectedTrigger]);
                        }
                        ImGui.Unindent();
                        ImGui.EndChild();

                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Advanced"))
                    {
                        var configValue = _workingCopy.LogChat;
                        if (ImGui.Checkbox("Log Chat to Debug", ref configValue))
                        {
                            _workingCopy.LogChat = configValue;

                        }
                        if (ImGui.Button("Restore Default Triggers"))
                        {
                            _workingCopy.LoadDefaults();
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }

                ImGui.Unindent(1); //for some reason the UI likes to cut off a pixel on the left side if we don't do this

                ImGui.EndChild();

                ////
                ////    FOOTER
                ////
                // save button
                if (ImGui.Button("Save"))
                {
                    _configuration.Import(_workingCopy);
                    _configuration.Save();
                    Service.Plugin.Reload();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Save configuration changes to disk.");
                }
                // end save button
                ImGui.SameLine();
                // apply button
                if (ImGui.Button("Apply"))
                {
                    _configuration.Import(_workingCopy);
                    Service.Plugin.Reload();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Apply configuration changes without saving.");
                }
                // end apply button
                ImGui.SameLine();
                // revert button
                if (ImGui.Button("Revert"))
                {
                    try
                    {
                        var cloneconfig = _configuration.CloneConfigurationFromDisk();
                        _configuration.Import(cloneconfig);
                        _workingCopy.Import(_configuration);
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Error(ex, "Could not restore configuration.");
                    }

                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Discard all changes and reload the configuration from disk.");
                }
                // end revert button
            }

            ImGui.End();
        }

        private void DrawTriggerConfig(dynamic t)
        {
            switch (t)
            {
                case ChatTriggerConfig config:
                    DrawChatTriggerConfig(config);
                    break;
                case EmoteTriggerConfig config:
                    DrawEmoteTriggerConfig(config);
                    break;
                default:
                    Service.PluginLog.Error($"Trigger type {t.Type} is not supported.");
                    break;
            }
        }

        /// <summary>
        /// Draws the configuration interface for chat triggers
        /// </summary>
        /// <param name="t">A ChatTriggerConfig object containing the current configuration for the trigger.</param>
        private void DrawChatTriggerConfig(dynamic t)
        {
            if (ImGui.BeginTabBar("TriggerConfig", ImGuiTabBarFlags.None))
            {


                DrawChatTriggerBasicTab(t);

                DrawTriggerDevicesTab(t);

                if (t.UseFilter)
                {
                    DrawChatTriggerFilterTab(t);
                }

                DrawTriggerPatternTab(t);


                ImGui.EndTabBar();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        private void DrawChatTriggerBasicTab(ChatTriggerConfig t)
        {
            if (ImGui.BeginTabItem("Basic"))
            {

                //begin name field
                var name = t.Name;
                if (ImGui.InputText("Name", ref name, 64))
                {
                    t.Name = name;
                }
                //end name field

                //begin regex field
                var regex = t.Regex;
                if (ImGui.InputText("Regex", ref regex, 255))
                {
                    t.Regex = regex;
                }
                //end regex field

                //begin retrigger delay field
                var retriggerdelay = (int)t.RetriggerDelay;
                if (ImGui.InputInt("Retrigger Delay (ms)", ref retriggerdelay))
                {
                    t.RetriggerDelay = retriggerdelay;
                }
                //end retrigger delay field
                var usefilter = t.UseFilter;
                if (ImGui.Checkbox("Use Filters", ref usefilter))
                {
                    t.UseFilter = usefilter;
                }

                ImGui.EndTabItem();
            }
        }

        /// <summary>
        /// Draws the configuration interface for chat triggers
        /// </summary>
        /// <param name="t">A ChatTriggerConfig object containing the current configuration for the trigger.</param>
        private void DrawEmoteTriggerConfig(EmoteTriggerConfig t)
        {
            if (ImGui.BeginTabBar("TriggerConfig", ImGuiTabBarFlags.None))
            {
                DrawEmoteTriggerBasicTab(t);
                DrawTriggerDevicesTab(t);
                DrawTriggerPatternTab(t);

                ImGui.EndTabBar();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        private void DrawEmoteTriggerBasicTab(EmoteTriggerConfig t)
        {
            if (ImGui.BeginTabItem("Basic"))
            {

                //begin name field
                var name = t.Name;
                if (ImGui.InputText("Name", ref name, 64))
                {
                    t.Name = name;
                }
                //end name field

                ImGui.Separator();

                ImGui.Text("Trigger Emotes:");
                if (t.EmoteIds.Count == 0)
                {
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "None selected");
                }
                else
                {
                    List<ushort> toRemove = [];
                    foreach (var emoteId in t.EmoteIds)
                    {
                        var selectedEmote = EmoteDataUtil.GetEmote(emoteId);
                        ImGui.Bullet();
                        ImGui.TextUnformatted(selectedEmote?.Name.ExtractText() ?? selectedEmote?.TextCommand.ValueNullable?.Command.ExtractText() ?? (emoteId > 0 ? $"ID {emoteId}" : null) ?? "Missing data");
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            using (ImRaii.PushId(emoteId))
                            {
                                // You have no idea how much I want to somehow create an alias named `SmolButton`.
                                if (ImGui.SmallButton(FontAwesomeIcon.TrashAlt.ToIconString()))
                                {
                                    toRemove.Add(emoteId);
                                }
                            }
                        }
                    }

                    foreach (var emoteId in toRemove)
                    {
                        t.EmoteIds.Remove(emoteId);
                    }
                }

                //begin emote ID field
                if (ImGui.Button("Add Emote"))
                    _emoteSelectionModal.OpenModalPopup();

                if (_emoteSelectionModal.DrawEmoteSelectionPopup("SelectEmotePopup", null, out var newEmoteId))
                {
                    if (!t.EmoteIds.Contains((ushort)newEmoteId))
                    {
                        t.EmoteIds.Add((ushort)newEmoteId);
                    }
                }
                //end emote ID field

                ImGui.NewLine();

                ImGui.TextUnformatted("Activate this trigger when you are the:");
                var triggerOnPerform = t.TriggerOnPerform;
                if (ImGui.Checkbox("Performer", ref triggerOnPerform))
                {
                    t.TriggerOnPerform = triggerOnPerform;
                }

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    using (ImRaii.Tooltip())
                    {
                        ImGui.Text("Trigger when you perform the emote.");
                        ImGui.TextDisabled("Ex: you /dote on someone.");
                    }
                }

                var triggerOnTarget = t.TriggerOnTarget;
                if (ImGui.Checkbox("Target", ref triggerOnTarget))
                {
                    t.TriggerOnTarget = triggerOnTarget;
                }

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    using (ImRaii.Tooltip())
                    {
                        ImGui.TextUnformatted("Trigger when you are the target");
                        ImGui.TextUnformatted("of someone performing the emote.");
                        ImGui.TextDisabled("Ex: someone /dotes on you.");
                    }
                }

                if (!t.TriggerOnPerform && !t.TriggerOnTarget)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                    {
                        ImGui.TextWrapped("This trigger will never activate if neither of the options are selected!");
                    }
                }

                ImGui.Separator();

                //begin retrigger delay field
                var retriggerDelay = (int)t.RetriggerDelay;
                if (ImGui.InputInt("Retrigger Delay (ms)", ref retriggerDelay))
                {
                    t.RetriggerDelay = retriggerDelay;
                }
                //end retrigger delay field

                ImGui.EndTabItem();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        private void DrawTriggerDevicesTab(TriggerConfig t)
        {
            ////
            ////    DEVICES TAB
            ////
            if (ImGui.BeginTabItem("Devices"))
            {

                //Begin enabled devices selection
                _workingCopy!.SeenDevices = new List<string>(_configuration.SeenDevices);
                if (_workingCopy.SeenDevices.Count > 0)
                {
                    bool[] selected = new bool[_workingCopy.SeenDevices.Count];
                    bool modified = false;
                    foreach (var (device, j) in _workingCopy.SeenDevices.Select((value, i) => (value, i)))
                    {
                        if (t.EnabledDevices.Contains(device))
                        {
                            selected[j] = true;
                        }
                        else
                        {
                            selected[j] = false;
                        }
                    }
                    if (ImGui.BeginListBox("Enabled Devices"))
                    {
                        foreach (var (device, j) in _workingCopy.SeenDevices.Select((value, i) => (value, i)))
                        {
                            if (ImGui.Selectable(device, selected[j]))
                            {
                                selected[j] = !selected[j];
                                modified = true;
                            }
                        }
                        ImGui.EndListBox();
                    }
                    if (modified)
                    {
                        var toEnable = new List<string>();
                        foreach (var (device, j) in _workingCopy.SeenDevices.Select((value, i) => (value, i)))
                        {
                            if (selected[j])
                            {
                                toEnable.Add(device);
                            }
                        }
                        t.EnabledDevices = toEnable;
                    }
                }
                else
                {
                    ImGui.Text("Connect to Intiface and connect devices to populate the list.");
                }
                //end enabled devices selection

                ImGui.EndTabItem();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        private void DrawChatTriggerFilterTab(ChatTriggerConfig t)
        {
            if (ImGui.BeginTabItem("Filters"))
            {
                if (ImGui.BeginCombo("##filtercategory", XIVChatFilter.FilterCategoryNames[_selectedFilterCategory]))
                {
                    var k = 0;
                    foreach (string name in XIVChatFilter.FilterCategoryNames)
                    {
                        if (name == "GM Messages")
                        {
                            // don't show the GM chat options for this filter configuration.
                            k++;
                            continue;
                        }

                        var isSelected = k == _selectedFilterCategory;
                        if (ImGui.Selectable(name, isSelected))
                        {
                            _selectedFilterCategory = k;
                        }
                        k++;
                    }
                    ImGui.EndCombo();
                }
                if (ImGui.BeginChild("filtercatlist", new Vector2(0, 0)))
                {
                    var i = 0;
                    bool modified = false;
                    foreach (string name in XIVChatFilter.FilterNames[_selectedFilterCategory])
                    {
                        if (name == "Novice Network" || name == "Novice Network Notifications")
                        {
                            // don't show novice network as selectable filters either.
                            i++;
                            continue;
                        }

                        bool filtersetting = t.FilterTable[_selectedFilterCategory][i];

                        if (ImGui.Checkbox(name, ref filtersetting))
                        {
                            modified = true;
                        }
                        if (modified)
                        {
                            t.FilterTable[_selectedFilterCategory][i] = filtersetting;
                        }
                        i++;
                    }
                    ImGui.EndChild();
                }
                ImGui.EndTabItem();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        private void DrawTriggerPatternTab(TriggerConfig t)
        {
            ////
            ////    PATTERN TAB
            ////
            if (ImGui.BeginTabItem("Pattern"))
            {
                //begin pattern selection
                if (ImGui.BeginCombo("##combo", t.PatternSettings!.Type))
                {
                    foreach (var pattern in IPatternType.All)
                    {
                        bool isSelected = t.PatternSettings.Type == pattern.Key;
                        if (ImGui.Selectable(pattern.Key, isSelected))
                        {
                            if (t.PatternSettings.Type != pattern.Key)
                            {
                                t.PatternSettings = pattern.Value.GetDefaultConfiguration();
                            }
                        }
                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }
                //end pattern selection

                ImGui.SameLine();

                //begin test button
                if (ImGui.ArrowButton("test", ImGuiDir.Right))
                {
                    Service.Plugin.DoPatternTest(t.PatternSettings);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Preview pattern on all devices.");
                }
                //end test button

                // begin preview plot
                try
                {
                    IPattern pattern = PatternFactory.GetPatternFromObject(t.PatternSettings);

                    float[] values = new float[t.PatternSettings.Duration];
                    for (int i = 0; i < t.PatternSettings.Duration; ++i)
                    {
                        values[i] = (float)pattern.GetIntensityAtTime(pattern.Expires - TimeSpan.FromMilliseconds(t.PatternSettings.Duration - i));
                    }
                    ImPlot.PushStyleVar(ImPlotStyleVar.FitPadding, new Vector2(0.3f, 0.3f));
                    if (ImPlot.BeginPlot("Preview", new Vector2(-1, 128)))
                    {
                        ImPlot.SetupAxis(ImAxis.X1, "Time", ImPlotAxisFlags.Lock | ImPlotAxisFlags.NoLabel);
                        ImPlot.SetupAxisLimits(ImAxis.X1, 0, t.PatternSettings.Duration, ImPlotCond.Always);
                        ImPlot.SetupAxis(ImAxis.Y1, "Intensity", ImPlotAxisFlags.Lock | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels);
                        ImPlot.SetupAxisLimits(ImAxis.Y1, 0, 1, ImPlotCond.Always);
                        unsafe
                        {
                            fixed (float* valuesPtr = values)
                            {
                                ImPlot.PlotShaded("", valuesPtr, values.Length, 0.0);
                            }
                        }
                        ImPlot.EndPlot();
                    }
                    ImPlot.PopStyleVar();
                }
                catch (Exception e)
                {
                    ImGui.Text("Unable to preview: " + e.Message);
                }
                // end preview plot

                ImGui.Indent();

                //begin pattern settings
                if (IPatternType.All.TryGetValue((string)t.PatternSettings.Type, out IPatternType? activeType))
                {
                    activeType.DrawSettings(t.PatternSettings);
                }
                else
                {
                    ImGui.Text("Select a valid pattern.");
                }
                //end pattern settings

                ImGui.Unindent();

                ImGui.EndTabItem();
            }
        }
    }
}
