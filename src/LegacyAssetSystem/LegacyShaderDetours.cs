using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;
using UnityEngine;

namespace RoR2BepInExPack.LegacyAssetSystem;

internal static class LegacyShaderDetours
{
    private static NativeDetour _shaderFindDetour;
    private delegate Shader ShaderFindDefinition(string path);
    private static ShaderFindDefinition _origFind;

    internal static void Init()
    {
        var shaderFindDetourConfig = new NativeDetourConfig { ManualApply = true };
        _shaderFindDetour = new NativeDetour(
                typeof(Shader).GetMethod(nameof(Shader.Find), ReflectionHelper.AllFlags, null, new[] { typeof(string) }, null),
                typeof(LegacyShaderDetours).GetMethod(nameof(OnShaderFind), ReflectionHelper.AllFlags),
                shaderFindDetourConfig
            );
        _origFind = _shaderFindDetour.GenerateTrampoline<ShaderFindDefinition>();
    }

    internal static void Enable()
    {
        _shaderFindDetour.Apply();
    }

    internal static void Disable()
    {
        _shaderFindDetour.Undo();
    }

    internal static void Destroy()
    {
        _shaderFindDetour.Free();
    }

    private static Shader OnShaderFind(string path)
    {
        var shader = LegacyShaderAPI.Find(path);
        if (shader)
        {
            return shader;
        }

        return _origFind(path);
    }
}
