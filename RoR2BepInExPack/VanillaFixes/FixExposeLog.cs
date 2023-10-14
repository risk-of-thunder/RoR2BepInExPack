using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using EntityStates;
using RoR2BepInExPack.Reflection;
using System;
using Mono.Cecil.Cil;
using UnityEngine;
using RoR2;
using static RoR2.RoR2Content;

namespace RoR2BepInExPack.VanillaFixes;


// Meaningless Logspam on any application of Expose.
// Fix: Don't.
internal class FixExposeLog
{
    private static ILHook _ilHook;


    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
                    typeof(HealthComponent).GetMethod(nameof(HealthComponent.TakeDamage),ReflectionHelper.AllFlags),
                    FixAddingExpose,
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

    private static void FixAddingExpose(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        ILLabel skipLabel = c.DefineLabel();
        bool ILFound = c.TryGotoNext(
                x => x.MatchLdarg(0),
                x => x.MatchLdfld(out _),
                x => x.MatchLdsfld(typeof(Buffs).GetField(nameof(Buffs.MercExpose))),
                x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod("AddBuff",new Type[]{typeof(BuffDef)})));
        c.MarkLabel(skipLabel);
        ILFound &= c.TryGotoPrev(
                x => x.MatchLdstr("Adding expose"));

        if (ILFound)
        {
            c.Emit(OpCodes.Br,skipLabel);
        }
        else
        {
            Log.Error("FixExposeLog TryGotoNext failed, not applying patch");
        }
    }
}
