global using UnityObject = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;
using UnityEngine;

namespace RoR2BepInExPack.LegacyAssetSystem;

internal static class LegacyResourcesDetours
{
    private static MethodInfo _legacyResourcesAPILoad;
    private static readonly Dictionary<Type, MethodInfo> GenericVersionsOfLegacyResourcesAPILoad = new();
    private static MethodInfo GetGenericLegacyResourcesAPILoad(Type type)
    {
        if (!GenericVersionsOfLegacyResourcesAPILoad.TryGetValue(type, out var genericLegacyResourcesAPILoad))
        {
            GenericVersionsOfLegacyResourcesAPILoad[type] = genericLegacyResourcesAPILoad = _legacyResourcesAPILoad.MakeGenericMethod(type);
        }

        return genericLegacyResourcesAPILoad;
    }

    private static NativeDetour _resourcesLoadDetour;
    private delegate UnityObject ResourcesLoadDefinition(string path, Type type);
    private static ResourcesLoadDefinition _origLoad;

    internal static void Init()
    {
        _legacyResourcesAPILoad = typeof(LegacyResourcesAPI).GetMethod(nameof(LegacyResourcesAPI.Load), ReflectionHelper.AllFlags);

        var resourcesLoadDetourConfig = new NativeDetourConfig { ManualApply = true };
        _resourcesLoadDetour = new NativeDetour(
                typeof(Resources).GetMethod(nameof(Resources.Load), ReflectionHelper.AllFlags, null, new[] { typeof(string), typeof(Type) }, null),
                typeof(LegacyResourcesDetours).GetMethod(nameof(OnResourcesLoad), ReflectionHelper.AllFlags),
                resourcesLoadDetourConfig
            );
        _origLoad = _resourcesLoadDetour.GenerateTrampoline<ResourcesLoadDefinition>();
    }

    internal static void Enable()
    {
        _resourcesLoadDetour.Apply();
    }

    internal static void Disable()
    {
        _resourcesLoadDetour.Undo();
    }

    internal static void Destroy()
    {
        _resourcesLoadDetour.Free();
    }

    private static UnityObject OnResourcesLoad(string path, Type type)
    {
        var legacyResourcesAPILoad = GetGenericLegacyResourcesAPILoad(type);

        var asset = (UnityObject)legacyResourcesAPILoad.Invoke(null, new[] { path });
        if (asset)
        {
            return asset;
        }

        return _origLoad(path, type);
    }
}
