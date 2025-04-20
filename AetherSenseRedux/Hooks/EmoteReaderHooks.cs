// The original implementation of this class was originally
// from the _Pat Me_ and _Emote Log_ plugins.
//
// It has been modified here to be more generic, and support
// notifications of both incoming and outgoing emotes.
//
// See:
//  - https://github.com/MgAl2O4/PatMeDalamud
//  - https://github.com/RokasKil/EmoteLog


using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;

namespace AetherSenseRedux.Hooks;

public class EmoteReaderHooks : IDisposable
{
    public delegate void EmoteDelegate(EmoteEvent e);

    private readonly Hook<OnEmoteFuncDelegate> hookEmote;

    public EmoteReaderHooks()
    {
        Service.PluginLog.Verbose("Before hook setup");
        hookEmote = Service.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>(
            "E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        Service.PluginLog.Verbose("Before hook enable");
        hookEmote.Enable();
        Service.PluginLog.Verbose("Hook enabled");
    }

    public void Dispose()
    {
        Service.PluginLog.Verbose("Emote reader dispose started");
        hookEmote.Dispose();
        GC.SuppressFinalize(this);
        Service.PluginLog.Verbose("Emote reader dispose complete");
    }

    public event EmoteDelegate? OnEmote;

    ~EmoteReaderHooks()
    {
        Service.PluginLog.Verbose("Emote reader destructor started");
        hookEmote.Dispose();
        Service.PluginLog.Verbose("Emote reader destructor complete");
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        // unk - some field of event framework singleton? doesn't matter here anyway
        Service.PluginLog.Verbose(
            $"Emote >> unk:{unk:X}, instigatorAddr:{instigatorAddr:X}, emoteId:{emoteId}, targetId:{targetId:X}, unk2:{unk2:X}, player:{Service.ClientState.LocalPlayer?.GameObjectId:X}");

        if (Service.ClientState.LocalPlayer != null)
        {
            var instigatorOb = Service.ObjectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr);
            if (instigatorOb is IPlayerCharacter playerCharacter)
            {
                // If a remote player performed the emote while targeting the local player
                if (targetId == Service.ClientState.LocalPlayer.GameObjectId)
                {
                    Service.PluginLog.Debug(
                        $"Player {instigatorOb.Name} used emote {emoteId} on target {Service.ClientState.LocalPlayer.Name} ({targetId:X})");
                    OnEmote?.Invoke(new EmoteEvent
                    {
                        EmoteId = emoteId,
                        Instigator = playerCharacter,
                        Target = Service.ClientState.LocalPlayer
                    });
                }
                // If the local player performed the emote
                else if (instigatorOb.GameObjectId == Service.ClientState.LocalPlayer.GameObjectId)
                {
                    var targetOb = targetId != 0xE0000000
                        ? Service.ObjectTable.FirstOrDefault(x => x.GameObjectId == targetId)
                        : null;

                    Service.PluginLog.Debug(
                        $"Local player {instigatorOb.Name} used emote {emoteId}" + (targetOb != null
                            ? $" on target {targetOb.Name} ({targetId:X})"
                            : string.Empty));

                    OnEmote?.Invoke(new EmoteEvent
                    {
                        EmoteId = emoteId,
                        Instigator = Service.ClientState.LocalPlayer,
                        Target = targetOb is IPlayerCharacter targetPc ? targetPc : null
                    });
                }
            }
        }

        hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId,
        ulong unk2);
}

public class EmoteEvent
{
    /// <summary>
    ///     The character which performed the emote.
    /// </summary>
    public required IPlayerCharacter Instigator { get; set; }

    /// <summary>
    ///     The character who was the target of the emote.
    /// </summary>
    public IPlayerCharacter? Target { get; set; }

    /// <summary>
    ///     The emote ID.
    /// </summary>
    public ushort EmoteId { get; set; }
}