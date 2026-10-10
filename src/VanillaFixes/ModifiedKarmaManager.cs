using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HG.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ExpansionManagement;
using RoR2.Karma;
using RoR2BepInExPack.Reflection;

using static RoR2.KarmaManager;

namespace RoR2BepInExPack.VanillaFixes;

// Unfortunately the KarmaManager uses AchievementManager code to search for BaseKarmaTrigger types, instead of the searchable attribute system
// We can improve KarmaManager.BuildKarmaTriggers in a few ways:
// * Catch any exceptions thrown by GetCustomAttribute.
// * Only search assemblies with the SearchableAttribute.OptIn attribute for karma triggers, so that:
//  - We don't load otherwise unused assemblies, like the legacy MMHOOK assembly
//  - We don't waste time scanning assemblies that will never have karma triggers, like MMHOOK, RoR2BepInExPack, or Unity libs
internal class ModifiedKarmaManager
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _hook = new Hook(
                        typeof(KarmaManager).GetMethod(nameof(BuildKarmaTriggers), ReflectionHelper.AllFlags),
                        typeof(ModifiedKarmaManager).GetMethod(nameof(BetterBuildKarmaTriggers), ReflectionHelper.AllFlags),
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

    // Fully replaces KarmaManager.BuildKarmaTriggers. See class comments for details, and code comments for specific changes
    private static void BetterBuildKarmaTriggers()
    {
        karmaTriggers = []; // from the og method - not needed, but best to leave it for future-proofing

        // Changed: don't scan duplicate assemblies
        HashSet<string> visitedAssemblies = [];
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Changed: only scan assemblies that opt in
            if (assembly.GetCustomAttribute<SearchableAttribute.OptInAttribute>() == null)
            {
                continue;
            }
            if (!visitedAssemblies.Add(assembly.FullName))
            {
                Log.Warning($"{nameof(ModifiedKarmaManager)}: Not scanning an assembly for karma triggers because it has the same name as {assembly.FullName}");
                continue;
            }
            foreach (Type karmaTriggerType in from type in assembly.GetTypes()
                                              where type != null && type.IsSubclassOf(typeof(BaseKarmaTrigger))
                                              orderby type.Name
                                              select type)
            {
                // Changed: safely wrap the GetCustomAttribute call (and use GetCustomAttribute instead of GetCustomAttributes)
                RegisterKarmaTriggerAttribute registerKarmaTriggerAttribute = null;
                try
                {
                    registerKarmaTriggerAttribute = karmaTriggerType.GetCustomAttribute<RegisterKarmaTriggerAttribute>(false);
                }
                catch (Exception ex)
                {
                    Log.Error($"{nameof(ModifiedKarmaManager)}: GetCustomAttribute<RegisterKarmaTriggerAttribute> failed for {karmaTriggerType.FullName}\n{ex}");
                }
                if (registerKarmaTriggerAttribute != null)
                {
                    ExpansionDef requiredExpansion = !string.IsNullOrEmpty(registerKarmaTriggerAttribute.requiredExpansionName) ? ExpansionCatalog.FindByName(registerKarmaTriggerAttribute.requiredExpansionName) : null;
                    KarmaTriggerDef karmaTriggerDef = new KarmaTriggerDef
                    {
                        identifier = registerKarmaTriggerAttribute.identifier,
                        baseKarmaReward = registerKarmaTriggerAttribute.karmaReward,
                        requiredExpansion = requiredExpansion,
                        type = karmaTriggerType
                    };
                    karmaTriggers.Add(karmaTriggerDef);
                }
            }
        }

        triggerNamesToDefs.Clear();
        for (int index = 0; index < karmaTriggers.Count; index++)
        {
            karmaTriggers[index].karmaTriggerIndex = new KarmaTriggerIndex
            {
                intValue = index
            };
            triggerNamesToDefs.Add(karmaTriggers[index].identifier, karmaTriggers[index]);
        }
    }
}
