using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace RoR2BepInExPack.VanillaFixes;

// When the PostProcessManager inits during startup, it scans every type in EVERY assembly to find PostProcessEffectSettings types
// This is obviously very slow, and also loads otherwise unused assemblies like the legacy MMHOOK dll, which massively inflates load times
// Fix: Filter out MMHOOK assemblies; only search assemblies which directly depend on Unity.Postprocessing.Runtime (including itself)
// Originally from RiskOfWaiting, which also included a cache for the type search, but the benefits of the cache were nominal
internal class FixPostProcessManager
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _hook = new Hook(
                        typeof(PostProcessManager).GetMethod(nameof(PostProcessManager.ReloadBaseTypes), ReflectionHelper.AllFlags),
                        typeof(FixPostProcessManager).GetMethod(nameof(BetterReloadBaseTypes), ReflectionHelper.AllFlags),
                        hookConfig
                    );
    }

    internal static void Enable()
    {
        _hook.Apply();
    }

    internal static void Disable()
    {
        _hook.Undo();
    }

    internal static void Destroy()
    {
        _hook.Free();
    }

    private static void BetterReloadBaseTypes(PostProcessManager ppManager)
    {
        ppManager.CleanBaseTypes();

        Assembly postProcessingAssembly = typeof(PostProcessEffectSettings).Assembly;
        Assembly ror2BepInExPackAssembly = typeof(FixPostProcessManager).Assembly;

        RegisterTypesInAssembly(ppManager, postProcessingAssembly);

        // UnityExplorer creates a second RoR2 assembly for some reason; we need to track duplicate assemblies
        HashSet<string> visitedAssemblies = [postProcessingAssembly.FullName];
        string postProcessingAssemblyName = postProcessingAssembly.GetName().Name;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // This mod needs to depend on Unity.Postprocessing.Runtime, but it will never include PostProcessEffectSettings
            if (assembly.FullName.StartsWith("MMHOOK_") || assembly == ror2BepInExPackAssembly)
            {
                continue;
            }
            foreach (var referenceAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (referenceAssemblyName.Name == postProcessingAssemblyName)
                {
                    if (visitedAssemblies.Add(assembly.FullName))
                    {
                        RegisterTypesInAssembly(ppManager, assembly);
                    }
                    else
                    {
                        Log.Warning($"Not scanning an assembly for PostProcessing because it has the same name as {assembly.FullName}");
                    }
                    break;
                }
            }
        }
    }

    private static void RegisterTypesInAssembly(PostProcessManager ppManager, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(PostProcessEffectSettings)) || type.IsAbstract)
            {
                continue;
            }
            PostProcessAttribute ppAttribute = type.GetCustomAttribute<PostProcessAttribute>(false);
            if (ppAttribute == null)
            {
                continue;
            }
            ppManager.settingsTypes.Add(type, ppAttribute);
            PostProcessEffectSettings baseSettings = (PostProcessEffectSettings)ScriptableObject.CreateInstance(type);
            baseSettings.SetAllOverridesTo(state: true, excludeEnabled: false);
            ppManager.m_BaseSettings.Add(baseSettings);
        }
    }
}
