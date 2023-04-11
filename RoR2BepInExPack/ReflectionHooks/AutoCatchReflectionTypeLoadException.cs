using System;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.ReflectionHooks;

// Lot of code around that call Assembly.GetTypes or similar methods and never handle ReflectionTypeLoadException properly.
// Fix: Catch it for them and return non null types
internal class AutoCatchReflectionTypeLoadException
{
    private static Hook _onHook;

    internal static void Init()
    {
        var ilHookConfig = new HookConfig() { ManualApply = true };
        _onHook = new Hook(
                    typeof(Assembly).GetMethods(ReflectionHelper.AllFlags).
                    First(
                        m => m.Name == nameof(Assembly.GetTypes) && m.GetParameters().Length == 0 &&
                        (m.MethodImplementationFlags & MethodImplAttributes.InternalCall) == 0),
                    typeof(AutoCatchReflectionTypeLoadException).GetMethod(nameof(AutoCatchReflectionTypeLoadException.SaferGetTypes), ReflectionHelper.AllFlags),
                    ref ilHookConfig
                );
    }

    internal static void Enable()
    {
        _onHook.Apply();
    }

    internal static void Disable()
    {
        _onHook.Undo();
    }

    internal static void Destroy()
    {
        _onHook.Free();
    }

    private static Type[] SaferGetTypes(Func<Assembly, Type[]> orig, Assembly self)
    {
        var types = Array.Empty<Type>();

        try
        {
            types = orig(self);
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray();
            Log.Debug($"Assembly.GetTypes() failed for {self.FullName} (threw ReflectionTypeLoadException). {e}");
        }

        return types;
    }
}
