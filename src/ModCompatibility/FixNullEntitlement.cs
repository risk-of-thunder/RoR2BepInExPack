using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using EntityStates;
using RoR2BepInExPack.Reflection;
using System;
using Mono.Cecil.Cil;
using UnityEngine;
using RoR2.EntitlementManagement;

namespace RoR2BepInExPack.ModCompatibility;


// Mods don't often have their own entitlement defs because creating them is unnecessarily complex,but the game assumes they exist if expansions are involved.
// Fix: Everyone is entitled to having a Nothing.
internal class FixNullEntitlement
{
    private static ILHook _localILHook;
    private static ILHook _networkILHook;


    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _localILHook = new ILHook(
                    typeof(BaseUserEntitlementTracker<RoR2.LocalUser>).GetMethod(nameof(BaseUserEntitlementTracker<RoR2.LocalUser>.UserHasEntitlement),ReflectionHelper.AllFlags),
                    FixEntitledCheck,
                    ref ilHookConfig
                );
        _networkILHook = new ILHook(
                    typeof(BaseUserEntitlementTracker<RoR2.NetworkUser>).GetMethod(nameof(BaseUserEntitlementTracker<RoR2.NetworkUser>.UserHasEntitlement),ReflectionHelper.AllFlags),
                    FixEntitledCheck,
                    ref ilHookConfig
                );
    }

    internal static void Enable()
    {
        _localILHook.Apply();
        _networkILHook.Apply();
    }

    internal static void Disable()
    {
        _localILHook.Undo();
        _networkILHook.Undo();
    }

    internal static void Destroy()
    {
        _localILHook.Free();
        _networkILHook.Free();
    }

    private static void FixEntitledCheck(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        bool ILFound = c.TryGotoNext(
            x => x.MatchLdstr("entitlementDef"));

        if (ILFound)
        {
            c.Emit(OpCodes.Ldc_I4_1);
            c.Emit(OpCodes.Ret);
        }
        else
        {
            Log.Error("FixNullEntitlement TryGotoNext failed, not applying patch");
        }
    }
}
