using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// CharacterBody.RemoveOldestTimedBuff doesn't remove a buff
// if it's index in the CharacterBody.timedBuffs array is 0,
// because of incorrect guard-clause
// Fix: exit the method only if index is < 0.
internal class FixCharacterBodyRemoveOldestTimedBuff
{
    private static ILHook _ilHook;

    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
            typeof(CharacterBody).GetMethod(nameof(CharacterBody.RemoveOldestTimedBuff), ReflectionHelper.AllFlags, null, new[] { typeof(BuffIndex) }, null),
            FixRemoveOldestTimedBuff,
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

    private static void FixRemoveOldestTimedBuff(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        bool ILFound = c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(1),
                x => x.MatchLdcI4(0),
                x => x.MatchBle(out _));

        if (ILFound)
        {
            c.Previous.OpCode = OpCodes.Blt;
        }
        else
        {
            Log.Error("FixRemoveOldestTimedBuff TryGotoNext failed, not applying patch");
        }
    }
}
