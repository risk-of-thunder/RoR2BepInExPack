using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;
using UnityEngine;

namespace RoR2BepInExPack.ModCompatibility;

// Lot of code around that call WWise methods without checking if the WWise module is in the process
// Fix: Properly check and safely return so that the process doesnt get a native engine crash
internal class SaferWWise
{
    private static List<Hook> _hooks = new();

    internal static void Init()
    {
        foreach (var akSoundEngineMethod in
            typeof(AkSoundEngine).GetMethods(BindingFlags.Public | BindingFlags.Static).
            Where(m => m.ReturnParameter.ParameterType == typeof(AKRESULT)))
        {
            var onHookConfig = new HookConfig() { ManualApply = true };
            _hooks.Add(new Hook(
                akSoundEngineMethod,
                typeof(SaferWWise).GetMethod(nameof(EarlyReturnIfNoSoundEngine), ReflectionHelper.AllFlags),
                ref onHookConfig));
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

    private static AKRESULT EarlyReturnIfNoSoundEngine(Func<AKRESULT> orig)
    {
        if (Application.isBatchMode)
            return AKRESULT.AK_Fail;

        return orig();
    }
}
