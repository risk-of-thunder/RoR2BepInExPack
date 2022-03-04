using MonoMod.RuntimeDetour;
using RoR2;
using System.Reflection;
using UnityEngine;

namespace RoR2BepInExPack;

internal static class LegacyShaderDetours
{
    private static NativeDetour _shaderFindDetour;
    private delegate Shader ShaderFindDefinition(string path);
    private static ShaderFindDefinition _origFind;

    internal static void Init()
    {
        const BindingFlags allFlags = (BindingFlags)(-1);

        var shaderFindDetourConfig = new NativeDetourConfig { ManualApply = true };
        _shaderFindDetour = new NativeDetour(
                typeof(Shader).GetMethod(nameof(Shader.Find), allFlags, null, new[] { typeof(string) }, null),
                typeof(LegacyShaderDetours).GetMethod(nameof(OnShaderFind), allFlags),
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
