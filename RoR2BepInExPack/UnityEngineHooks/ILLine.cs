using System;
using System.Diagnostics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// Original code from mistername

internal class ILLine
{
    private static ILHook _hook;

#if DEBUG
    private static Hook _testHook;
#endif

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new ILHook(typeof(StackTrace).GetMethod("AddFrames", ReflectionHelper.AllFlags), new ILContext.Manipulator(ShowILLine));

#if DEBUG
        _testHook = new Hook(typeof(ILLine).GetMethod("TestIlLinedmd", ReflectionHelper.AllFlags), TestDmdHook);

        try
        {
            TestIlLinedmd();
        }
        catch (Exception ex)
        {
            Log.Error($"Testing IL Lines from DMDs{Environment.NewLine}{ex}");
        }
#endif
    }

#if DEBUG
    internal static void TestDmdHook(Action orig)
    {
        orig();

        throw new Exception("test exception dmd");
    }

    internal static void TestIlLinedmd()
    {
        Log.Debug("ILLine TestIlLinedmd called.");
    }
#endif

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

    // Replaces the call to GetFileLineNumber to a call to GetLineOrIL
    private static void ShowILLine(ILContext il)
    {
        var cursor = new ILCursor(il);

        try
        {
            cursor.GotoNext(
                x => x.MatchCallOrCallvirt(typeof(StackFrame).GetMethod("GetFileLineNumber", ReflectionHelper.AllFlags))
            );

            cursor.RemoveRange(2);
            cursor.EmitDelegate(GetLineOrIL);
        }
        catch (Exception ex)
        {
            Log.Error($"{nameof(ShowILLine)} hook 1 failed.{Environment.NewLine}{ex}");
        }

        try
        {
            cursor.Index = 0;

            int frameLocIndex = -1;
            cursor.GotoNext(
                MoveType.After,
                x => x.MatchLdloc(out frameLocIndex),
                x => x.MatchCallOrCallvirt(typeof(StackFrame).GetMethod("GetInternalMethodName", ReflectionHelper.AllFlags))
            );

            cursor.Emit(OpCodes.Ldloc, frameLocIndex);
            cursor.EmitDelegate(AppendLineOrIL);

        }
        catch (Exception ex)
        {
            Log.Error($"{nameof(ShowILLine)} hook 2 failed.{Environment.NewLine}{ex}");
        }
    }

    // First gets the debug line number (C#) and only if that is not available returns the IL offset (jit might change it a bit)
    private static string GetLineOrIL(StackFrame instance)
    {
        var line = instance.GetFileLineNumber();
        if (line != StackFrame.OFFSET_UNKNOWN && line != 0)
        {
            return line.ToString();
        }

        return "IL_" + instance.GetILOffset().ToString("X4");
    }

    private static string AppendLineOrIL(string currentText, StackFrame instance)
    {
        return currentText + " (at " + GetLineOrIL(instance) + ")";
    }
}
