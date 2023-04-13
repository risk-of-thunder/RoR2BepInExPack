
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using EntityStates;
using RoR2BepInExPack.Reflection;
using System;
using Mono.Cecil.Cil;
using UnityEngine;
using RoR2.Items;
using System.Reflection;
using RoR2;

namespace RoR2BepInExPack.VanillaFixes;

// The ProjectileCatalog logs an error if more than 256 projectiles are registered, despite the actual limit being much higher.
// Fix: Skip the projectile limit check to prevent the misleading error
internal class FixProjectileCatalogLimitError
{
    private static ILHook _ilHook;

    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
                    typeof(ProjectileCatalog).GetMethod(nameof(ProjectileCatalog.SetProjectilePrefabs), ReflectionHelper.AllFlags),
                    IncreaseCatalogLimit,
                    ref ilHookConfig
                );
    }
    internal static void Enable()
    {
        _ilHook.Apply();
    }
    internal static void Disable()
    {
        _ilHook.Undo();
    }
    internal static void Destroy()
    {
        _ilHook.Free();
    }
    private static void IncreaseCatalogLimit(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        int locLimitIndex = -1;
        ILLabel breakLabel = c.DefineLabel();
        bool ILFound = c.TryGotoNext(MoveType.Before,
            x => x.MatchLdcI4(0x100),
            x => x.MatchStloc(out locLimitIndex),
            x => x.MatchLdsfld(typeof(ProjectileCatalog).GetField(nameof(ProjectileCatalog.projectilePrefabs), ReflectionHelper.AllFlags)),
            x => x.MatchLdlen(),
            x => x.MatchConvI4(),
            x => x.MatchLdloc(locLimitIndex),
            x => x.MatchBle(out breakLabel)
            );

        if (ILFound)
        {
            c.Emit(OpCodes.Br, breakLabel);
        }
        else
        {
            Log.Error("IncreaseCatalogLimit TryGotoNext failed, not applying patch");
        }
    }
}
