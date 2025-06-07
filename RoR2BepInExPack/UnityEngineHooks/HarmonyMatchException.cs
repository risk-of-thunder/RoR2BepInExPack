using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HarmonyLib.Internal.Util;
using HarmonyLib.Public.Patching;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace RoR2BepInExPack.VanillaFixes;

// Original code from mistername

[HarmonyPatch]
internal class HarmonyMatchException
{
    private static Harmony _harmony;
    private static ILHook _hook;

#if DEBUG
    private static Hook _testHook;
#endif

    internal static void Init()
    {
        foreach (var method in typeof(HarmonyManipulator).GetMethods())
        {
            Log.Warning(method.Name);
        }
        _hook = new ILHook(AccessTools.DeclaredMethod(typeof(HarmonyManipulator), "ApplyILManipulators"), FixHarmonyMatchException, new ILHookConfig() { ManualApply = true });

#if DEBUG
        _testHook = new Hook(AccessTools.EnumeratorMoveNext(typeof(HarmonyMatchException).GetMethod(nameof(HarmonyMatchException.TestDmdHook), ~BindingFlags.Default)), Hookthing);

        try
        {
            _harmony = new Harmony(nameof(HarmonyMatchException));
        }
        catch (Exception ex)
        {
            Log.Error($"Testing IL Lines from DMDs{Environment.NewLine}{ex}");
        }
#endif
    }

#if DEBUG
    [HarmonyPatch(typeof(HarmonyMatchException), nameof(HarmonyMatchException.TestDmdHook), MethodType.Enumerator)]
    [HarmonyPrefix]
    public static void ItemDisplayRuleSet_GenerateRuntimeValuesAsync7(ILContext il)
    {
        var c = new ILCursor(il);
        c.TryFindNext(out _, x => x.MatchBrfalse(out _), x => x.MatchBr(out _));
    }
    [HarmonyPatch(typeof(HarmonyMatchException), nameof(HarmonyMatchException.TestDmdHook), MethodType.Enumerator)]
    [HarmonyILManipulator]
    public static void ItemDisplayRuleSet_GenerateRuntimeValuesAsyncs(ILContext il)
    {
        var c = new ILCursor(il);
        c.TryFindNext(out _, x => x.MatchBrfalse(out _), x => x.MatchBr(out _));
    }
    [HarmonyPatch(typeof(HarmonyMatchException), nameof(HarmonyMatchException.TestDmdHook), MethodType.Enumerator)]
    [HarmonyPrefix]
    public static void ItemDisplayRuleSet_GenerateRuntimeValuesAsyncss()
    {
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
    public static bool Hookthing(Func<IEnumerator, bool> orig, IEnumerator self)
    {
        return orig(self);
    }
#endif

    internal static void Enable()
    {
        _hook.Apply();
        _harmony.CreateClassProcessor(typeof(HarmonyMatchException)).Patch();
    }

    internal static void Disable()
    {
        _hook.Undo();
        _harmony.UnpatchSelf();
    }

    internal static void Destroy()
    {
        _hook.Free();
    }

    // Replaces the call to GetFileLineNumber to a call to GetLineOrIL
    public static void FixHarmonyMatchException(ILContext il)
    {
        var c = new ILCursor(il) { Index = il.Instrs.Count - 1};
        c.GotoPrev(MoveType.AfterLabel, x => x.MatchRet());
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate(UnApplyILLabels);
        /*
			IL_00ea: callvirt instance object [mscorlib]System.Reflection.MethodBase::Invoke(object, object[])
			IL_00ef: pop
*/
        c = new ILCursor(il);
        c.GotoNext(MoveType.After,
               x => x.MatchCallvirt(AccessTools.Method(typeof(MethodBase), nameof(MethodBase.Invoke), [typeof(object), typeof(object[])])),
               x => x.MatchPop()
            );
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate(ApplyILLabels);
    }

    public static void ApplyILLabels(ILContext il)
    {
        foreach (var instr in il.Instrs)
        {
            if (instr.Operand is Instruction target)
                instr.Operand = il.DefineLabel(target);
            else if (instr.Operand is Instruction[] targets)
                instr.Operand = targets.Select(il.DefineLabel).ToArray();
        }

    }
    public static void UnApplyILLabels(ILContext il)
    {
        foreach (var instr in il.Instrs)
        {
            if (instr.Operand is ILLabel label)
                instr.Operand = label.Target;
            else if (instr.Operand is ILLabel[] targets)
                instr.Operand = targets.Select(l => l.Target).ToArray();
        }

    }
}
