namespace AetherSenseRedux.Hooks;

// The original implementation of this class was originally
// from the _Pat Me_ and _Emote Log_ plugins.
//
// It has been modified here to be more generic, and support
// notifications of both incoming and outgoing emotes.
//
// See:
//  - https://github.com/MgAl2O4/PatMeDalamud
//  - https://github.com/RokasKil/EmoteLog


using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using System;
using System.Linq;

public class EmoteReaderHooks : IDisposable
{
    public delegate void EmoteDelegate(IPlayerCharacter playerCharacter, ushort emoteId);

    public event EmoteDelegate? OnEmote;

    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> hookEmote;

    public EmoteReaderHooks()
    {
        hookEmote = Service.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        hookEmote.Enable();
    }

    public void Dispose()
    {
        hookEmote?.Dispose();
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        // unk - some field of event framework singleton? doesn't matter here anyway
        Service.PluginLog.Info($"Emote >> unk:{unk:X}, instigatorAddr:{instigatorAddr:X}, emoteId:{emoteId}, targetId:{targetId:X}, unk2:{unk2:X}, player:{Service.ClientState.LocalPlayer?.GameObjectId:X}");

        if (Service.ClientState.LocalPlayer != null)
        {
            var instigatorOb = Service.ObjectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr);
            if (instigatorOb is IPlayerCharacter playerCharacter)
            {
                if (targetId == Service.ClientState.LocalPlayer.GameObjectId)
                {
                    OnEmote?.Invoke(playerCharacter, emoteId);
                }
                else if (instigatorOb.GameObjectId == Service.ClientState.LocalPlayer.GameObjectId)
                {
                    Service.PluginLog.Info($"Local player {instigatorOb.Name} used emote {emoteId} on target {targetId:X}");
                }
                else
                {
                    Service.PluginLog.Info($"I DONT KNOW {instigatorOb.GameObjectId:X} {instigatorOb.Name}");
                }
            }

        }

        hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }
}