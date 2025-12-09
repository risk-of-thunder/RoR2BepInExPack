using System;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using RoR2BepInExPack.Reflection;
using UnityEngine.AddressableAssets;

namespace RoR2BepInExPack.VanillaFixes;

// todo: FIX THIS - it relies on food tier being the most recent one added by vanilla
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
            // please dont change this anymore gbx i beg of you
            ItemTier realAssignedAtRuntime = (ItemTier)1000;

            // stupid!
            ItemTierDef foodTier = Addressables.LoadAssetAsync<ItemTierDef>(RoR2_DLC3.FoodTier_asset).WaitForCompletion();

            foreach (ItemTierDef itemTierDef in ContentManager.itemTierDefs)
            {
                // the hardcoding is insane but whatever man!
                if (itemTierDef._tier >= (ItemTier)10 && itemTierDef._tier != realAssignedAtRuntime)
                {
                    // ensure food tier is placed correctly, yeet everything else
                    if (itemTierDef != foodTier)
                    {
                        itemTierDef._tier = realAssignedAtRuntime;
                    }
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
