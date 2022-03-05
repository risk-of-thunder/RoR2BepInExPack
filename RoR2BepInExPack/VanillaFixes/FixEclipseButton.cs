using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.Reflection;
using System;

namespace RoR2BepInExPack.VanillaFixes;

// Temporary fix until the Eclipse Button in the main menu is correctly set by the game devs.
// It gets disabled when modded even though this option is currently singleplayer only.
internal class FixEclipseButton
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new Hook(
                        typeof(DisableIfGameModded).GetMethod(nameof(DisableIfGameModded.OnEnable), ReflectionHelper.AllFlags),
                        typeof(FixConsoleLog).GetMethod(nameof(FixEclipseButton.FixIt), ReflectionHelper.AllFlags),
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

    private static void FixIt(Action<DisableIfGameModded> orig, DisableIfGameModded self)
    {
        if (self.name == "GenericMenuButton (Eclipse)")
        {
            var button = self.GetComponent<MPButton>();
            if (button)
            {
                button.defaultFallbackButton = true;
            }
        }
        else
        {
            orig(self);
        }
    }
}
