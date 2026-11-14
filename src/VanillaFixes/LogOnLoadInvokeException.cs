using System;
using System.Linq;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;

namespace RoR2BepInExPack.VanillaFixes;

/// <summary>
/// An invoked method that throws while getting resolved, e.g., MissingMemberException, doesn't appear on the stack.
/// Instead of just getting the error at the Invoke calling site, we can log which invocation specifically errored to
/// avoid a trial-and-error search for the culprit.
/// </summary>
internal class LogOnLoadInvokeException
{
    private static ILHook _ilHook;

    internal static void Init()
    {
        _ilHook = new ILHook(
            typeof(RoR2Application).GetMethod(nameof(RoR2Application.InitializeGameRoutine), Reflection.ReflectionHelper.AllFlags).GetStateMachineTarget(),
            LogInvokeException,
            new ILHookConfig { ManualApply = true });
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

    private static void LogInvokeException(ILContext il)
    {
        var c = new ILCursor(il);
        if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld<RoR2Application>(nameof(RoR2Application.onLoad)),
                x => x.MatchCallOrCallvirt(out _)))
        {
            c.Prev.Operand = typeof(LogOnLoadInvokeException).GetMethod(nameof(InvokeAndLogException), Reflection.ReflectionHelper.AllFlags);
        }
        else
        {
            Log.Error($"Failed to patch {il.Method.Name}");
        }
    }

    private static void InvokeAndLogException(Action onLoad)
    {
        foreach (var action in onLoad.GetInvocationList().Cast<Action>())
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                throw new TargetInvocationException($"Error in 'RoR2Application.onLoad()' when invoking {action.Method.DeclaringType}.{action.Method.Name}", ex);
            }
        }
    }
}
