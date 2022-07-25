using BepInEx.Configuration;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using RoR2BepInExPack.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using System.Runtime.CompilerServices;

namespace RoR2BepInExPack.ModCompatibility;

internal class FixMultiCorrupt
{
    private static ILHook _ilHook;

    private static readonly ConditionalWeakTable<Inventory, Dictionary<ItemIndex, int>> _alternateTracker = new();
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
        _ilHook = new ILHook(
                    typeof(ContagiousItemManager).GetMethod(nameof(ContagiousItemManager.StepInventoryInfection),ReflectionHelper.AllFlags),
                    FixStep,
                    ref ilHookConfig
                );
    }

    internal static void Enable()
    {
        _ilHook.Apply();
    }

    internal static void Disable()
    {
        _ilHook.Undo();
    }

    internal static void Destroy()
    {
        _ilHook.Free();
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
                        var dict = _alternateTracker.GetOrCreateValue(inventory);

                        if(!dict.ContainsKey(pureItem))
                            dict.Add(pureItem, 0);

                        return possibilities[dict[pureItem]++ % possibilities.Count];

                    case ContagionPriority.Rarest:
                        possibilities.Sort((item, item2) => ItemCatalog.GetItemDef(item).tier.CompareTo(ItemCatalog.GetItemDef(item2).tier));
                        return possibilities.Last();

                    case ContagionPriority.Random:
                        if (_voidRNG == null)
                        {
                            _voidRNG = new Xoroshiro128Plus(Run.instance.stageRng.nextUlong);
                        }

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
}
