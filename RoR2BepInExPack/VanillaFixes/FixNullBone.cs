using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using EntityStates;
using RoR2BepInExPack.Reflection;
using System;
using Mono.Cecil.Cil;
using UnityEngine;

namespace RoR2BepInExPack.VanillaFixes;


// Dynamic Bone system wrongly assumes that all bones are valid at all times.
// Fix: Make sure they are before doing anything with them.
internal class FixNullBone
{
    private static ILHook _ilHook;


    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
                    typeof(DynamicBone).GetMethod(nameof(DynamicBone.ApplyParticlesToTransforms),ReflectionHelper.AllFlags),
                    FixBoneCheck,
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

    private static void FixBoneCheck(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        ILLabel ifLabel = c.DefineLabel();
        int localIndex = -1;
        bool ILFound = c.TryGotoNext(
            x => x.MatchCallOrCallvirt(out _),
            x => x.MatchLdcI4(1),
            x => x.MatchBgt(out ifLabel)
            )&& c.TryGotoPrev(
            x => x.MatchStloc(out localIndex),
            x => x.MatchLdloc(localIndex));

        if (ILFound)
        {
          c.Index++;
          c.Emit(OpCodes.Ldloc,localIndex);
          c.EmitDelegate<Func<DynamicBone.Particle,bool>>((p) => p?.m_Transform);
          c.Emit(OpCodes.Brfalse,ifLabel);
        }
        else
        {
            Log.Error("FixNullBone TryGotoNext failed, not applying patch");
        }
    }
}
