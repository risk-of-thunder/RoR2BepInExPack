
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

// BaseItemBodyBehavior logs targeted methods returning a null ItemDef as an error when this is usually intentional for modded item behaviors 
// Fix: Skip logging the error if the item behavior is not from RoR2
internal class FixItemDefReturnedNullLog
{
    private static ILHook _ilHook;

    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
                    typeof(BaseItemBodyBehavior).GetMethod(nameof(BaseItemBodyBehavior.Init),ReflectionHelper.AllFlags),
                    SkipModdedReturnedNullErrors,
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
    private static void SkipModdedReturnedNullErrors(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        ILLabel breakLabel = c.DefineLabel();
        int locMethodIndex = -1;
        bool ILFound = c.TryGotoNext(MoveType.After,
            x => x.MatchBr(out breakLabel),
            x => x.MatchLdloc(out locMethodIndex),
            x => x.MatchLdnull(),
            x => x.MatchCallOrCallvirt<Array>(nameof(Array.Empty)),
            x => x.MatchCallOrCallvirt<MethodBase>(nameof(MethodBase.Invoke)),
            x => x.MatchCastclass<ItemDef>()
            ) && c.TryGotoNext(MoveType.After,
            x => x.MatchCallOrCallvirt<UnityEngine.Object>("op_Implicit"),
            x => x.MatchBrtrue(out _)
            );

        if (ILFound)
        {
            c.Emit(OpCodes.Ldloc, locMethodIndex);
            c.EmitDelegate<Func<MethodInfo, bool>>((method) => method != null && method.DeclaringType.Assembly != typeof(BaseItemBodyBehavior).Assembly);
            c.Emit(OpCodes.Brtrue, breakLabel);
        }
        else
        {
            Log.Error("SkipModdedReturnedNullErrors TryGotoNext failed, not applying patch");
        }
    }
}
