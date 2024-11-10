using System;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.Reflection;
using UnityEngine.UI;

namespace RoR2BepInExPack.VanillaFixes;

// Interface added to toggle tutorial mode has invalid range check, and overflows outside 
// parent. This causes issues as soon as any other difficulty is added. Solution is to 
// reintroduce missing layout element and find the correct index.
internal class FixRuleBookViewerStrip
{
    private static Hook _hook = null;

    internal static void Init()
    {
        _hook ??= new Hook(
                typeof(RuleBookViewerStrip).GetMethod(
                        nameof(RuleBookViewerStrip.SetData), ReflectionHelper.AllFlags),
                typeof(FixRuleBookViewerStrip).GetMethod(
                        nameof(FindIndexAndSetWidth), ReflectionHelper.AllFlags),
                new HookConfig() { ManualApply = true }
            );
    }

    internal static void Enable()
    {
        _hook?.Apply();
    }

    internal static void Disable()
    {
        _hook?.Undo();
    }

    internal static void Destroy()
    {
        _hook?.Free();
        _hook = null;
    }

    private static void FindIndexAndSetWidth(
            Action<RuleBookViewerStrip, List<RuleChoiceDef>, int> orig,
            RuleBookViewerStrip self, List<RuleChoiceDef> choices, int index)
    {
        orig(self, choices, index);

        for (int i = 0, count = choices.Count; i < count; ++i)
        {
            if (index == choices[i].localIndex)
            {
                self.currentDisplayChoiceIndex = i;
                break;
            }
        }

        if (!self.GetComponent<LayoutElement>())
        {
            self.gameObject.AddComponent<LayoutElement>().minWidth = 64;
        }
    }
}
