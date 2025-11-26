using System;
using System.Linq;
using MonoMod.RuntimeDetour;
using RoR2.UI;
using RoR2.UI.MainMenu;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// When vanilla game hides Prismatic trials it's still a default button for controllers,
// because of that you can't select anything else, because navigation doesn't work
// Fix: set defaultFallbackButton for Eclipse button
internal class FixExtraGameModesMenu
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new Hook(
                        typeof(MainMenuController).GetMethod(nameof(MainMenuController.Start), ReflectionHelper.AllFlags),
                        typeof(FixExtraGameModesMenu).GetMethod(nameof(FixExtraGameModesMenu.FixIt), ReflectionHelper.AllFlags),
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

    private static void FixIt(Action<MainMenuController> orig, MainMenuController self)
    {
        orig(self);

        var buttons = self.extraGameModeMenuScreen.GetComponentsInChildren<MPButton>();
        var eclipseButton = buttons.FirstOrDefault(b => b.name == "GenericMenuButton (Eclipse)");
        if (eclipseButton)
        {
            eclipseButton.defaultFallbackButton = true;
        }
    }
}
