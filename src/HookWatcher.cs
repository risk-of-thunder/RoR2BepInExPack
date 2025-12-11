using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MonoDetour;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack;

#nullable enable

internal static class HookWatcher
{
    private static DetourModManager ModManager { get; set; } = null!;

    private static Hook _harmonyWatcher = null!;

    private static bool _isMonoDetourPresent = false;

    internal static void Init()
    {
        try
        {
            _isMonoDetourPresent = Type.GetType("MonoDetour.MonoDetourHook, com.github.MonoDetour")?.GetMethod("TryGetFrom") is not null;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        ModManager = new DetourModManager();

        ModManager.OnHook += LogOnHook;
        ModManager.OnILHook += LogILHook;

        ModManager.OnDetour += LogDetour;
        ModManager.OnNativeDetour += LogNativeDetour;

        HookEndpointManager.OnAdd += LogHookAdd;
        HookEndpointManager.OnModify += LogHookModify;
        HookEndpointManager.OnRemove += LogHookRemove;

        _harmonyWatcher = new Hook(
            typeof(PatchJobs<MethodInfo>.Job).GetMethod(nameof(PatchJobs<MethodInfo>.Job.AddPatch), ReflectionHelper.AllFlags),
            HarmonyAddPatchWatcher, new HookConfig { ManualApply = true });
        _harmonyWatcher.Apply();
    }

    private static void HarmonyAddPatchWatcher(Action<PatchJobs<MethodInfo>.Job, AttributePatch> orig,
        PatchJobs<MethodInfo>.Job self, AttributePatch patch)
    {
        orig(self, patch);

        try
        {
            var patchAss = patch.info.method.DeclaringType.Assembly;
            var assemblyName = string.IsNullOrEmpty(patchAss.Location) ? patchAss.GetName().Name : Path.GetFileName(patchAss.Location);
            Log.Debug($"Harmony {patch.type} {patch.info.method.FullDescription()} added by {assemblyName} for: {self.original.FullDescription()}");
        }
        catch (Exception e)
        {
            Log.Debug(e);
        }
    }

    internal static void Destroy()
    {
        _harmonyWatcher.Undo();
        _harmonyWatcher.Free();

        HookEndpointManager.OnRemove -= LogHookRemove;
        HookEndpointManager.OnModify -= LogHookModify;
        HookEndpointManager.OnAdd -= LogHookAdd;

        ModManager.OnNativeDetour -= LogNativeDetour;
        ModManager.OnDetour -= LogDetour;

        ModManager.OnILHook -= LogILHook;
        ModManager.OnHook -= LogOnHook;

        ModManager.Dispose();
        ModManager = null!;
    }

    private static void LogOnHook(Assembly hookOwner, MethodBase from, MethodBase to, object target)
        => LogHook(new() { Kind = HookInfo.HookKind.On, Owner = hookOwner, OriginalManaged = from, HookMethodBase = to });

    private static void LogILHook(Assembly hookOwner, MethodBase from, ILContext.Manipulator manipulator)
    {
        if (_isMonoDetourPresent && IfMonoDetourHookDoSpecializedLog(manipulator, from))
            return;

        LogHook(new() { Kind = HookInfo.HookKind.IL, Owner = hookOwner, OriginalManaged = from, HookDelegate = manipulator });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool IfMonoDetourHookDoSpecializedLog(ILContext.Manipulator manipulator, MethodBase from)
    {
        if (!MonoDetourHook.TryGetFrom(manipulator, out var hook))
            return false;

        string applierTypeName;
        if (hook.ApplierType.Name.EndsWith("Detour", StringComparison.InvariantCulture))
            applierTypeName = hook.ApplierType.Name[..^6];
        else
            applierTypeName = hook.ApplierType.Name;

        LogHook(
            new() { Kind = HookInfo.HookKind.IL, Owner = hook.Manipulator.Module.Assembly, OriginalManaged = from, HookMethodBase = hook.Manipulator },
            specifier: $" MonoDetour<{applierTypeName}>"
        );

        return true;
    }

    private static void LogDetour(Assembly hookOwner, MethodBase from, MethodBase to)
        => LogHook(new() { Kind = HookInfo.HookKind.On, Owner = hookOwner, OriginalManaged = from, HookMethodBase = to });

    private static void LogNativeDetour(Assembly hookOwner, MethodBase originalMethod, IntPtr from, IntPtr to)
        => LogHook(new() { Kind = HookInfo.HookKind.Native, Owner = hookOwner, OriginalNative = from, HookIntPtr = to });

    private static bool LogHookAdd(MethodBase from, Delegate to)
    {
        var info = GetHookInfo(from, to);

        return LogHook(info);
    }

    private static bool LogHookModify(MethodBase from, Delegate to)
    {
        var info = GetHookInfo(from, to);

        // Seems to be only used by IL Manipulators?
        return LogHook(info, "modifier");
    }

    private static bool LogHookRemove(MethodBase from, Delegate to)
    {
        var info = GetHookInfo(from, to);

        return LogHook(info, "removed");
    }

    private static HookInfo GetHookInfo(MethodBase from, Delegate to)
    {
        var info = new HookInfo()
        {
            Owner = to.Method.Module.Assembly,
            OriginalManaged = from,
        };

        if (to is ILContext.Manipulator manipulator)
        {
            info.Kind = HookInfo.HookKind.IL;
            info.HookDelegate = manipulator;
        }
        else
        {
            info.Kind = HookInfo.HookKind.On;
            info.HookDelegate = to;
        }

        return info;
    }

    internal class HookInfo
    {
        internal enum HookKind
        {
            On,
            IL,
            Native,
        }

        internal HookKind Kind;

        internal Assembly? Owner;

        internal MethodBase? OriginalManaged;
        internal IntPtr OriginalNative;

        internal Delegate? HookDelegate;
        internal IntPtr HookIntPtr;
        internal MethodBase? HookMethodBase;
    }

    internal static bool RedirectFixFrameRateDependantLogicHooks = false;
    private static bool LogHook(HookInfo hookInfo, string context = "added", string? specifier = null)
    {
        if (hookInfo.OriginalManaged == null)
        {
            return true;
        }

        var hookOwnerDllName = "Not Found";
        if (hookInfo.Owner != null)
        {
            // Get the dll name instead of the assembly manifest name, as the latter may not be correctly defined by the mod creator.
            hookOwnerDllName = Path.GetFileName(hookInfo.Owner.Location);
        }

        var fromDeclaringType = hookInfo.OriginalManaged.DeclaringType;
        var fromName = hookInfo.OriginalManaged.Name;
        var fromIdentifier = fromDeclaringType != null ? $"{fromDeclaringType.FullName}.{fromName}" : fromName;

        string? GetToIdentifier()
        {
            if (hookInfo.HookDelegate != null)
            {
                var toDeclaringType = hookInfo.HookDelegate.Method?.DeclaringType;
                var toName = hookInfo.HookDelegate.Method?.Name;
                return toDeclaringType != null ? $"{toDeclaringType.FullName}.{toName}" : toName;
            }

            if (hookInfo.HookMethodBase != null)
            {
                var toDeclaringType = hookInfo.HookMethodBase.DeclaringType;
                var toName = hookInfo.HookMethodBase.Name;
                return toDeclaringType != null ? $"{toDeclaringType.FullName}.{toName}" : toName;
            }

            return "";
        }

        Log.Debug($"{hookInfo.Kind}Hook{specifier} {GetToIdentifier()} {context} by assembly: {hookOwnerDllName} for: {fromIdentifier}");

        return true;
    }
}
