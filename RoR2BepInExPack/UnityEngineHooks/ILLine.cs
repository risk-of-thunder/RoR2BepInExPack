using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;
using System;
using System.Diagnostics;

namespace RoR2BepInExPack.VanillaFixes;

// Original code from mistername

internal class ILLine
{
    private static ILHook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new ILHook(typeof(StackTrace).GetMethod("AddFrames", ReflectionHelper.AllFlags), new ILContext.Manipulator(ShowILLine));
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

    // Replaces the call to GetFileLineNumber to a call to GetLineOrIL
    private static void ShowILLine(ILContext il)
    {
        try
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(
                x => x.MatchCallOrCallvirt(typeof(StackFrame).GetMethod("GetFileLineNumber", ReflectionHelper.AllFlags))
            );

            cursor.RemoveRange(2);
            cursor.EmitDelegate(GetLineOrIL);
        }
        catch (Exception ex)
        {
            Log.Error($"{nameof(ShowILLine)} hook failed.{Environment.NewLine}{ex}");
        }
    }

    // First gets the debug line number (C#) and only if that is not available returns the IL offset (jit might change it a bit)
    private static string GetLineOrIL(StackFrame instace)
    {
        var line = instace.GetFileLineNumber();
        if (line != StackFrame.OFFSET_UNKNOWN && line != 0)
        {
            return line.ToString();
        }

        return "IL_" + instace.GetILOffset().ToString("X4");
    }
}
