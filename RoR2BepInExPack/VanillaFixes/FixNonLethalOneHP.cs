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


// When the player has non zero but less than one health the game fumbles the float math and kills anyways. 
// Fix: Check for positive health instead of >= 1,the emitted delegate to do so is a bit roundabout to keep the existing branch instructions happy.
internal class FixNonLethalOneHP
{
    private static ILHook _ilHook;


    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
                    typeof(HealthComponent).GetMethod(nameof(HealthComponent.TakeDamage),ReflectionHelper.AllFlags),
                    FixLethality,
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

    private static void FixLethality(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        bool ILFound = c.TryGotoNext( MoveType.After,
                x => x.MatchLdfld(typeof(HealthComponent).GetField(nameof(HealthComponent.health),ReflectionHelper.AllFlags)),
                x => x.MatchLdcR4(1),
                x => x.MatchBltUn(out _));

        if (ILFound)
        {
            c.Index--;
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float,HealthComponent,float>>((targetHealth,self) => (self.health > 0f) ? 0f : targetHealth); 
        }
        else
        {
            Log.Error("FixNonLethalOneHP TryGotoNext failed, not applying patch");
        }
    }
}
