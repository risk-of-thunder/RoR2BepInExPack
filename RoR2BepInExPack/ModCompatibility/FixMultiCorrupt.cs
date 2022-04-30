using BepInEx;
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

using static RoR2.Items.ContagiousItemManager;

namespace RoR2BepInExPack.ModCompatibility;

internal class FixMultiCorrupt
{

    private static ILHook _ilhook;

    private static ConditionalWeakTable<Inventory,Dictionary<ItemIndex,int>> alternateTracker = new ConditionalWeakTable<Inventory, Dictionary<ItemIndex, int>>();
    private static Xoroshiro128Plus voidRNG = null;
    private static ConfigEntry<ContagionPriority> contagionPriority;

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
        contagionPriority = config.Bind<ContagionPriority>("General","Contagion Priority",ContagionPriority.Random,"Determines behaviour for when multiple results are available for an item transformation.");
        var ilhookConfig = new ILHookConfig() {ManualApply = true};
        _ilhook = new ILHook(
                        typeof(ContagiousItemManager).GetMethod(nameof(ContagiousItemManager.StepInventoryInfection),ReflectionHelper.AllFlags),
                        FixMultiCorrupt.FixStep,
                        ref ilhookConfig
                    );
    }

    internal static void Enable()
    {
        _ilhook.Apply();
    }

    internal static void Disable()
    {
        _ilhook.Undo();
    }

    internal static void Destroy()
    {
        _ilhook.Free();
    }

    internal static void FixStep(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        bool ILFound = c.TryGotoNext(MoveType.After,
            x => x.MatchLdsfld(typeof(ContagiousItemManager).GetField(nameof(ContagiousItemManager.originalToTransformed),ReflectionHelper.AllFlags)),
            x => x.MatchLdarg(1),
            x => x.MatchLdelemI4());

        if(ILFound){
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldarg,0);
            c.Emit(OpCodes.Ldarg,1);
            c.EmitDelegate<Func<Inventory,ItemIndex,ItemIndex>>((inventory,pureItem) =>{
                List<ItemIndex> possibilities = inventory.itemAcquisitionOrder.Where(item => ContagiousItemManager.GetOriginalItemIndex(item) == pureItem).ToList();
                    switch(contagionPriority.Value){
                        case ContagionPriority.First:
                          return possibilities.First();
                        case ContagionPriority.Last:
                          return possibilities.Last();
                        case ContagionPriority.Alternate:
                          var dict = alternateTracker.GetOrCreateValue(inventory);
                          if(!dict.ContainsKey(pureItem))
                            dict.Add(pureItem,0);
                          return possibilities[(dict[pureItem]++)%(possibilities.Count)];
                        case ContagionPriority.Rarest:
                          possibilities.Sort((item,item2) => ItemCatalog.GetItemDef(item).tier.CompareTo(ItemCatalog.GetItemDef(item2).tier));
                          return possibilities.Last();
                        case ContagionPriority.Random:
                          if(voidRNG == null){
                            voidRNG = new Xoroshiro128Plus(RoR2.Run.instance.stageRng.nextUlong);
                          }
                          return possibilities[voidRNG.RangeInt(0,possibilities.Count)];
                        default:      
                          return ContagiousItemManager.originalToTransformed[(int)pureItem];
                    }
            });
        }
    }

  
}
