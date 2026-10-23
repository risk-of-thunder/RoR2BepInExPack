using System;
using MonoMod.RuntimeDetour;
using RoR2.ContentManagement;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// Fixes CanLoadType so JunkContent doesn't load additional assets in modded games by default.
// Loading these extra assets break multiplayer compatibility with vanilla game instances.
// Mods that need this content can load it explicitly via Addressables.Load.
// Alternatively, loadAll can be used to restore the old behavior of all loading all assets and registering them in their respective catalogs.
internal static class FixMultiplayerCompatibilityWithVanilla
{
    private static Hook _hook;
    internal static bool loadAll = false;

    internal static void Init()
    {
        var hookConfig = new HookConfig { ManualApply = true };
        _hook = new Hook(
            typeof(AddressablesLoadHelper).GetMethod(nameof(AddressablesLoadHelper.CanLoadType), ReflectionHelper.AllFlags),
            Fix,
            ref hookConfig);
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

    private static bool Fix(Func<AddressablesLoadHelper, string, ContentPack, bool> orig, AddressablesLoadHelper self, string _type, ContentPack contentPack)
    {
        if (contentPack.loadOnlyExplicitTypes.Count >= 1)
        {
            return loadAll || contentPack.loadOnlyExplicitTypes.Contains(_type);
        }

        return true;
    }
}
