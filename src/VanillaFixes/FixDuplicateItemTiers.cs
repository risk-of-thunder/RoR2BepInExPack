using System;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// 10 broke from initial 1.4 patch
// 11 broke from 1.4.1 patch
// 1000 is now assigned at runtime
internal class FixDuplicateItemTiers
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new Hook(
                        typeof(ItemTierCatalog).GetMethod(nameof(ItemTierCatalog.Init), ReflectionHelper.AllFlags),
                        typeof(FixDuplicateItemTiers).GetMethod(nameof(FixDuplicateItemTiers.ReassignDuplicateTiers), ReflectionHelper.AllFlags),
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

    private static void ReassignDuplicateTiers(Action orig)
    {
        try
        {
            List<ItemTierDef> vanillaTiers = new List<ItemTierDef>();
            foreach (ReadOnlyContentPack contentPack in ContentManager.allLoadedContentPacks)
            {
                // Ignore vanilla tiers
                if (contentPack.identifier.StartsWith("RoR2."))
                {
                    vanillaTiers.AddRange(contentPack.itemTierDefs);
                }
            }

            foreach (ItemTierDef itemTierDef in ContentManager.itemTierDefs)
            {
                if (!vanillaTiers.Contains(itemTierDef))
                {
                    // Ensure all modded tiers have the correct AssignedAtRuntime value
                    itemTierDef._tier = ItemTier.AssignedAtRuntime;
                }
            }
        }
        catch (Exception e )
        {
            Log.Error(e);
        }
        
        orig();
    }
}
