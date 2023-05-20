using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;
using UnityEngine.Networking;

namespace RoR2BepInExPack.VanillaFixes;

// Run difficulty scaling isnt properly computed on run start
internal static class FixRunScaling
{
    private static ILHook _ilHook;

    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig { ManualApply = true };
        _ilHook = new ILHook(
            typeof(Run).GetMethod(nameof(Run.Start), ReflectionHelper.AllFlags),
            CalculateDifficultyCoefficientOnRunStart,
            ref ilHookConfig);
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

    private static void CalculateDifficultyCoefficientOnRunStart(ILContext il)
    {
        var c = new ILCursor(il);

        bool ILFound = c.TryGotoNext(MoveType.After,
            i => i.MatchStfld<Run>(nameof(Run.allowNewParticipants)));

        if (ILFound)
        {
            ILFound = c.TryGotoNext(MoveType.After,
            i => i.MatchStfld<Run>(nameof(Run.allowNewParticipants)));

            if (ILFound)
            {
                c.Emit(OpCodes.Ldarg_0);
                static void CallRecalculateDifficultyCoefficent(Run instance)
                {
                    if (NetworkServer.active && instance)
                    {
                        instance.RecalculateDifficultyCoefficent();
                    }
                }
                c.EmitDelegate(CallRecalculateDifficultyCoefficent);
            }
            else
            {
                Log.Error("CalculateDifficultyCoefficientOnRunStart TryGotoNext 2 failed, not applying patch");
            }
        }
        else
        {
            Log.Error("CalculateDifficultyCoefficientOnRunStart TryGotoNext 1 failed, not applying patch");
        }
    }
}
