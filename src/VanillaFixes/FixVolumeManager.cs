using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoR2BepInExPack.VanillaFixes;

// When the VolumeManager lazy inits, it scans every type in EVERY assembly to find VolumeComponent types
// This is obviously very slow, and also loads otherwise unused assemblies like the legacy MMHOOK dll
// Same issue as PostProcessManager but doesn't occur during startup
// The VolumeManager lazy inits because the Winnowing of Mortality (soul1) scene mistakenly has some Volume components left active
// Fix: Filter out MMHOOK assemblies; only search assemblies which directly depend on Unity.RenderPipelines.Core.Runtime (including itself)
// Also make it a little safer
internal class FixVolumeManager
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _hook = new Hook(
                        typeof(VolumeManager).GetMethod(nameof(VolumeManager.ReloadBaseTypes), ReflectionHelper.AllFlags),
                        typeof(FixVolumeManager).GetMethod(nameof(BetterReloadBaseTypes), ReflectionHelper.AllFlags),
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

    private static void BetterReloadBaseTypes(VolumeManager self)
    {
        self.m_ComponentsDefaultState.Clear();
        var volumeComponentsTypesArray = GetVolumeComponentTypes();
        self.baseComponentTypeArray = volumeComponentsTypesArray;
        foreach (Type volumeComponentType in volumeComponentsTypesArray)
        {
            try
            {
                volumeComponentType.GetMethod("Init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);
                VolumeComponent defaultInstance = (VolumeComponent)ScriptableObject.CreateInstance(volumeComponentType);
                self.m_ComponentsDefaultState.Add(defaultInstance);
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(FixVolumeManager)}.{nameof(BetterReloadBaseTypes)} failed on {nameof(VolumeComponent)} type {volumeComponentType.FullName}:\n{ex}");
            }
        }
    }

    private static Type[] GetVolumeComponentTypes()
    {
        List<Type> volumeComponentTypes = [];

        Assembly renderingAssembly = typeof(VolumeComponent).Assembly;
        Assembly ror2BepInExPackAssembly = typeof(FixVolumeManager).Assembly;

        ScanAssembly(renderingAssembly);

        // UnityExplorer creates a second RoR2 assembly for some reason; we need to track duplicate assemblies
        HashSet<string> visitedAssemblies = [renderingAssembly.FullName];
        string renderingAssemblyName = renderingAssembly.GetName().Name;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // This mod needs to depend on Unity.RenderPipelines.Core.Runtime, but it will never include VolumeComponent types
            if (assembly.FullName.StartsWith("MMHOOK_") || assembly == ror2BepInExPackAssembly)
            {
                continue;
            }
            foreach (var referenceAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (referenceAssemblyName.Name == renderingAssemblyName)
                {
                    if (visitedAssemblies.Add(assembly.FullName))
                    {
                        ScanAssembly(assembly);
                    }
                    else
                    {
                        Log.Warning($"Not scanning an assembly for VolumeComponent types because it has the same name as {assembly.FullName}");
                    }
                    break;
                }
            }
        }

        return volumeComponentTypes.ToArray();

        void ScanAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsSubclassOf(typeof(VolumeComponent)) && !type.IsAbstract)
                {
                    volumeComponentTypes.Add(type);
                }
            }
        }
    }
}
