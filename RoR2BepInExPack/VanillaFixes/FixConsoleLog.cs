using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

internal class FixConsoleLog
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new Hook(
                        typeof(UnitySystemConsoleRedirector).GetMethod(nameof(UnitySystemConsoleRedirector.Redirect), ReflectionHelper.AllFlags),
                        typeof(FixConsoleLog).GetMethod(nameof(FixConsoleLog.DoNothing), ReflectionHelper.AllFlags),
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

    private static void DoNothing() { }
}
