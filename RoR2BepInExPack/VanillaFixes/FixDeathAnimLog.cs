
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using EntityStates;
using RoR2BepInExPack.Reflection;
using System;
using Mono.Cecil.Cil;
using UnityEngine;

namespace RoR2BepInExPack.VanillaFixes;


// GenericCharacterDeath queues up a death animation for any present animator,leading to unnecessary log output
// Fix: Ensure a death animation exists before trying to play it
internal class FixDeathAnimLog
{
    private static ILHook _ilHook;


    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
                    typeof(GenericCharacterDeath).GetMethod(nameof(GenericCharacterDeath.PlayDeathAnimation),ReflectionHelper.AllFlags),
                    FixLackingAnim,
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

    private static void FixLackingAnim(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        ILLabel label = c.DefineLabel();
        bool ILFound = c.TryGotoNext(MoveType.After,
            x => x.MatchLdloc(0),
            x => x.MatchCallOrCallvirt(out _),
            x => x.MatchBrfalse(out label));

        if (ILFound)
        {
            c.Emit(OpCodes.Ldloc_0);
            c.EmitDelegate<Func<Animator,bool>>((anim) => {
                for(int i = 0; i < anim.layerCount; i++){
                    if(anim.HasState(i,Animator.StringToHash("Death"))){
                        return true;
                    }
                }
              return false;
            });
            c.Emit(OpCodes.Brfalse,label);
        }
        else
        {
            Log.Error("FixDeathAnimLog TryGotoNext failed, not applying patch");
        }
    }
}
