using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace RoR2BepInExPack.ModCompatibility;

// Lot of code around that call WWise methods without checking if the WWise module is in the process
// Fix: Properly check and safely return so that the process doesnt get a native engine crash
internal class SaferWWise
{
    private static List<ILHook> _hooks = new();

    internal static void Init()
    {
        // Ideally this should check in the current process if the wwise native dll module is loaded
        var isWwiseNativeDllLoaded = !Application.isBatchMode;
        if (isWwiseNativeDllLoaded)
        {
            return;
        }

        foreach (var akSoundEngineMethod in
            typeof(AkSoundEngine).GetMethods(BindingFlags.Public | BindingFlags.Static).
            Where(m => m.ReturnParameter.ParameterType == typeof(AKRESULT)))
        {
            var hookConfig = new ILHookConfig() { ManualApply = true };
            _hooks.Add(new ILHook(
                akSoundEngineMethod,
                EarlyReturnIfNoSoundEngine,
                ref hookConfig));
        }
    }

    internal static void Enable()
    {
        foreach (var hook in _hooks)
        {
            hook.Apply();
        }
    }

    internal static void Disable()
    {
        foreach (var hook in _hooks)
        {
            hook.Undo();
        }
    }

    internal static void Destroy()
    {
        foreach (var hook in _hooks)
        {
            hook.Free();
        }
    }

    // early ret START
    // put 1 on stack if no sound
    // brfalse // if 0, jump after early ret
    // ldc i4 put akresult.fail on the stack
    // return
    // early ret END
    // rest of method
    private static void EarlyReturnIfNoSoundEngine(ILContext il)
    {
        var c = new ILCursor(il);

        static bool ReturnTrueIfNoSoundEngine()
        {
            return Application.isBatchMode;
        }

        c.EmitDelegate(ReturnTrueIfNoSoundEngine);

        var indexBeforeEarlyRet = c.Index;
        c.Emit(OpCodes.Ldc_I4, (int)AKRESULT.AK_Fail);
        c.Emit(OpCodes.Ret);

        var labelAfterRet = c.MarkLabel();

        c.Index = indexBeforeEarlyRet;
        c.Emit(OpCodes.Brfalse, labelAfterRet);
    }
}
