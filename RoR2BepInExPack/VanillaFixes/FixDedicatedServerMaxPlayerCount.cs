using System;
using MonoMod.RuntimeDetour;
using RoR2.Networking;
using RoR2BepInExPack.Reflection;
using UnityEngine.Networking;

namespace RoR2BepInExPack.VanillaFixes;

// Game is hard coding the max player count to 4 or some fake/unused max player count variable.
// Fix: Replace the bad code by the actual max player count.
internal class FixDedicatedServerMaxPlayerCount
{
    private static Hook _hook;
    private static Hook _hook2;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
#pragma warning disable CS0618 // Type or member is obsolete
        _hook = new Hook(
            typeof(NetworkManager).GetMethod(nameof(NetworkManager.StartHost), ReflectionHelper.AllFlags, null, [typeof(ConnectionConfig), typeof(int)], null),
            FixUsageOfMaxPlayerCountVariable,
            ref hookConfig
        );
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
        _hook2 = new Hook(
            typeof(NetworkManager).GetMethod(nameof(NetworkManager.StartServer), ReflectionHelper.AllFlags, null, [typeof(ConnectionConfig), typeof(int)], null),
            FixUsageOfMaxPlayerCountVariable2,
            ref hookConfig
        );
#pragma warning restore CS0618 // Type or member is obsolete
    }

    internal static void Enable()
    {
        _hook.Apply();
        _hook2.Apply();
    }

    internal static void Disable()
    {
        _hook2.Undo();
        _hook.Undo();
    }

    internal static void Destroy()
    {
        _hook2.Free();
        _hook.Free();
    }

#pragma warning disable CS0618 // Type or member is obsolete
    private static NetworkClient FixUsageOfMaxPlayerCountVariable(Func<NetworkManager, ConnectionConfig, int, NetworkClient> orig, NetworkManager self, ConnectionConfig config, int maxConnections)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        return orig(self, config, NetworkManagerSystem.SvMaxPlayersConVar.instance.intValue);
    }

#pragma warning disable CS0618 // Type or member is obsolete
    private static bool FixUsageOfMaxPlayerCountVariable2(Func<NetworkManager, ConnectionConfig, int, bool> orig, NetworkManager self, ConnectionConfig config, int maxConnections)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        return orig(self, config, NetworkManagerSystem.SvMaxPlayersConVar.instance.intValue);
    }
}
