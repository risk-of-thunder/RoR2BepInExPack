using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.Items;
using RoR2BepInExPack.Reflection;
using UnityEngine;

namespace RoR2BepInExPack.ModCompatibility;

internal class FixMultiCorrupt
{
    private static ILHook _stepInventoryInfectionILHook;
    private static Hook _generateStageRNGOnHook;

    private static Xoroshiro128Plus _voidRNG = null;
    private static ConfigEntry<ContagionPriority> _contagionPriority;

    internal enum ContagionPriority
    {
       First,
       Last,
       Random,
       Rarest,
       Alternate
    }

    internal static void Init(ConfigFile config)
    {
        _contagionPriority = config.Bind("General", "Contagion Priority", ContagionPriority.Random, "Determines behaviour for when multiple results are available for an item transformation.");
        
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _stepInventoryInfectionILHook = new ILHook(
                    typeof(ContagiousItemManager).GetMethod(nameof(ContagiousItemManager.StepInventoryInfection),ReflectionHelper.AllFlags),
                    FixStep,
                    ref ilHookConfig
                );

        var onHookConfig = new HookConfig() { ManualApply = true };
        _generateStageRNGOnHook = new Hook(
                    typeof(Run).GetMethod(nameof(Run.GenerateStageRNG), ReflectionHelper.AllFlags),
                    typeof(FixMultiCorrupt).GetMethod(nameof(OnGenerateStageRNG), ReflectionHelper.AllFlags),
                    ref onHookConfig
                );
    }

    internal static void Enable()
    {
        _stepInventoryInfectionILHook.Apply();
        _generateStageRNGOnHook.Apply();
    }

    internal static void Disable()
    {
        _stepInventoryInfectionILHook.Undo();
        _generateStageRNGOnHook.Undo();
    }

    internal static void Destroy()
    {
        _stepInventoryInfectionILHook.Free();
        _generateStageRNGOnHook.Free();
    }

    private static void FixStep(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        bool ILFound = c.TryGotoNext(MoveType.After,
            x => x.MatchLdsfld(typeof(ContagiousItemManager).GetField(nameof(ContagiousItemManager.originalToTransformed), ReflectionHelper.AllFlags)),
            x => x.MatchLdarg(1),
            x => x.MatchLdelemI4());

        if (ILFound)
        {
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldarg,0);
            c.Emit(OpCodes.Ldarg,1);
            c.EmitDelegate<Func<Inventory,ItemIndex,ItemIndex>>((inventory,pureItem) =>
            {
                List<ItemIndex> possibilities = inventory.itemAcquisitionOrder.Where(item => ContagiousItemManager.GetOriginalItemIndex(item) == pureItem).ToList();

                if (possibilities.Count == 0)
                {
                    possibilities = ContagiousItemManager.transformationInfos.Where((info) => info.originalItem == pureItem).Select((info) => info.transformedItem).ToList();
                }

                switch (_contagionPriority.Value)
                {
                    case ContagionPriority.First:
                        return possibilities.First();

                    case ContagionPriority.Last:
                        return possibilities.Last();

                    case ContagionPriority.Alternate:
                        var tracker = ContagionAlternateTracker.GetOrAdd(inventory);
                        return possibilities[tracker.AddContagion(pureItem) % possibilities.Count];

                    case ContagionPriority.Rarest:
                        possibilities.Sort((item, item2) => ItemCatalog.GetItemDef(item).tier.CompareTo(ItemCatalog.GetItemDef(item2).tier));
                        return possibilities.Last();

                    case ContagionPriority.Random:
                        return possibilities[_voidRNG.RangeInt(0,possibilities.Count)];

                    default:
                        return ContagiousItemManager.originalToTransformed[(int)pureItem];
                }
            });
        }
        else
        {
            Log.Error("FixMultiCorrupt TryGotoNext failed, not applying patch");
        }
    }

    private static void OnGenerateStageRNG(Action<Run> orig, Run self)
    {
        orig(self);

        _voidRNG = new Xoroshiro128Plus(self.stageRng.nextUlong);
    }

    private class ContagionAlternateTracker : MonoBehaviour
    {
        private static readonly Dictionary<Inventory, ContagionAlternateTracker> instances = new();
        private readonly Dictionary<ItemIndex, int> contagions = new();

        private Inventory inventory;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            instances[inventory] = this;
        }

        private void OnDestroy()
        {
            instances.Remove(inventory);
        }

        public int AddContagion(ItemIndex itemIndex)
        {
            if (contagions.TryGetValue(itemIndex, out var contagion))
            {
                return contagions[itemIndex] = contagion + 1;
            }

            return contagions[itemIndex] = 0;
        }

        public static ContagionAlternateTracker GetOrAdd(Inventory inventory)
        {
            if (instances.TryGetValue(inventory, out var instance))
            {
                return instance;
            }

            return inventory.gameObject.AddComponent<ContagionAlternateTracker>();
        }
    }
}
