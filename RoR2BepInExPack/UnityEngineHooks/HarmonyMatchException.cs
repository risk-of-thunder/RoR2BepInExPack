using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace RoR2BepInExPack.VanillaFixes;

[HarmonyPatch]
public class MatchFailTest
{
    [HarmonyPatch(typeof(HarmonyMatchException), nameof(HarmonyMatchException.TestDmdHook), MethodType.Enumerator)]
    [HarmonyPrefix]
    public static void Prefix()
    {
    }

    [HarmonyPatch(typeof(HarmonyMatchException), nameof(HarmonyMatchException.TestDmdHook), MethodType.Enumerator)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> TransRights(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var i in instructions)
            yield return i;
    }

    [HarmonyPatch(typeof(HarmonyMatchException), nameof(HarmonyMatchException.TestDmdHook), MethodType.Enumerator)]
    [HarmonyILManipulator]
    public static void ILHook(ILContext il)
    {
        var c = new ILCursor(il);
        ILCursor[] c2 = null;
        c.TryFindNext(out c2, x => x.MatchBrtrue(out _));
        c.TryFindNext(out c2, x => x.MatchBrfalse(out _));
        c.TryFindNext(out c2, x => x.MatchBr(out _));
    }
}

public class HarmonyMatchException
{
    private static ILHook _hook;
    private static Harmony _harmony;

    internal static void Init()
    {
        _harmony = new Harmony(nameof(HarmonyMatchException));

        var method = AccessTools.DeclaredMethod(typeof(HarmonyManipulator), "ApplyManipulators") ?? AccessTools.DeclaredMethod(typeof(HarmonyManipulator), "ApplyILManipulators");
        if (method is null)
            return;

        //_hook = new ILHook(method, FixHarmonyMatchException, new ILHookConfig { ManualApply = true });
    }
    internal static void Enable()
    {
        _hook?.Apply();
        _harmony.CreateClassProcessor(typeof(MatchFailTest)).Patch();
    }

    internal static void Disable()
    {
        _hook?.Undo();
        _harmony?.UnpatchSelf();
    }

    internal static void Destroy()
    {
        _hook?.Free();
        _harmony = null;
    }

    // Replaces the call to GetFileLineNumber to a call to GetLineOrIL
    public static void FixHarmonyMatchException(ILContext il)
    {
        var c = new ILCursor(il);
        c.GotoNext(MoveType.AfterLabel,
                x => x.MatchLdnull(),
                x => x.MatchLdloc(out _),
                x => x.MatchCallvirt(out _),
                x => x.MatchCallvirt(AccessTools.Method(typeof(MethodBase), nameof(MethodBase.Invoke), [typeof(object), typeof(object[])])),
                x => x.MatchPop());

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate(ApplyILLabels);

        c.GotoNext(MoveType.After, x => x.MatchPop());
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate(UnApplyILLabels);
    }

    public static void ApplyILLabels(ILContext il)
    {
        if (il?.Instrs is null)
            return;

        foreach (var instr in il.Instrs)
        {
            if (instr?.Operand is null)
                continue;

            if (instr.Operand is Instruction target)
                instr.Operand = il.DefineLabel(target);
            else if (instr.Operand is Instruction[] targets)
                instr.Operand = targets.Select(t => il.DefineLabel(t)).ToArray();
        }
    }

    public static void UnApplyILLabels(ILContext il)
    {
        if (il?.Instrs is null || il.IsReadOnly)
            return;

        foreach (var instr in il.Instrs)
        {
            if (instr?.Operand is null)
                continue;

            if (instr.Operand is ILLabel label)
                instr.Operand = label.Target;
            else if (instr.Operand is ILLabel[] targets)
                instr.Operand = targets.Select(l => l.Target).ToArray();
        }
    }

    public static IEnumerator TestDmdHook()
    {
        yield return null;

        if (true)
        {
            yield return null;
        }
        if (false)
        {
            yield return null;
        }
        yield break;
    }
}
