using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine.Networking;
using static Mono.Cecil.Cil.OpCodes;

namespace RoR2BepInExPack.VanillaFixes;
internal static class LogNetworkInvokeException
{
    private static IDetour _detour;

    internal static void Init()
    {
        _detour = new ILHook(
            typeof(NetworkConnection).GetMethod(nameof(NetworkConnection.InvokeDelegateWithCatch)),
            AddExceptionLogging,
            new ILHookConfig { ManualApply = true });
    }

    internal static void Enable()
    {
        _detour.Apply();
    }

    internal static void Disable()
    {
        _detour.Undo();
    }

    internal static void Destroy()
    {
        _detour.Free();
    }

    private static void AddExceptionLogging(ILContext il)
    {
        var c = new ILCursor(il);
        // goto catch (Exception)
        if (c.TryGotoNext(
            a => a.MatchLeaveS(out _),
            a => a.MatchPop(),
            a => a.MatchLdstr(out _)))
        {
            // store exception instead of popping it
            c.Index += 1;
            var exception = new VariableDefinition(c.Body.Method.Module.ImportReference(typeof(Exception)));
            c.Body.Variables.Add(exception);
            c.Next.Operand = exception;
            c.Next.OpCode = Stloc;

            // append exception to text
            c.Index += 2;
            c.Prev.Operand += Environment.NewLine + "{0}";
            c.Emit(Ldloc, exception);
            c.Emit(Call, ((Func<string, object, string>)string.Format).Method);
        }
        else
            Log.Error($"ILHook {nameof(LogNetworkInvokeException)}.{nameof(AddExceptionLogging)} failed.");
    }
}
