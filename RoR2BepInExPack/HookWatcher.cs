using System;
using System.IO;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using RoR2BepInExPack.Reflection;
using RoR2BepInExPack.VanillaFixes;

namespace RoR2BepInExPack;

internal static class HookWatcher
{
    private static DetourModManager ModManager { get; set; }

    internal static void Init()
    {
        ModManager = new DetourModManager();

        ModManager.OnHook += LogOnHook;
        ModManager.OnILHook += LogILHook;

        ModManager.OnDetour += LogDetour;
        ModManager.OnNativeDetour += LogNativeDetour;

        HookEndpointManager.OnAdd += LogHookAdd;
        HookEndpointManager.OnModify += LogHookModify;
        HookEndpointManager.OnRemove += LogHookRemove;
    }

    internal static void Destroy()
    {
        HookEndpointManager.OnRemove -= LogHookRemove;
        HookEndpointManager.OnModify -= LogHookModify;
        HookEndpointManager.OnAdd -= LogHookAdd;

        ModManager.OnNativeDetour -= LogNativeDetour;
        ModManager.OnDetour -= LogDetour;

        ModManager.OnILHook -= LogILHook;
        ModManager.OnHook -= LogOnHook;

        ModManager.Dispose();
        ModManager = null;
    }

    private static void LogOnHook(Assembly hookOwner, MethodBase from, MethodBase to, object target)
        => LogHookAndMaybeRedirect(new() { Kind = HookInfo.HookKind.On, Owner = hookOwner, OriginalManaged = from, HookMethodBase = to });

    private static void LogILHook(Assembly hookOwner, MethodBase from, ILContext.Manipulator manipulator)
        => LogHookAndMaybeRedirect(new() { Kind = HookInfo.HookKind.IL, Owner = hookOwner, OriginalManaged = from, HookDelegate = manipulator });

    private static void LogDetour(Assembly hookOwner, MethodBase from, MethodBase to)
        => LogHookAndMaybeRedirect(new() { Kind = HookInfo.HookKind.On, Owner = hookOwner, OriginalManaged = from, HookMethodBase = to });

    private static void LogNativeDetour(Assembly hookOwner, MethodBase originalMethod, IntPtr from, IntPtr to)
        => LogHookAndMaybeRedirect(new() { Kind = HookInfo.HookKind.Native, Owner = hookOwner, OriginalNative = from, HookIntPtr = to });

    private static bool LogHookAdd(MethodBase from, Delegate to)
    {
        var info = GetHookInfo(from, to);

        return LogHookAndMaybeRedirect(info);
    }

    private static bool LogHookModify(MethodBase from, Delegate to)
    {
        var info = GetHookInfo(from, to);

        // Seems to be only used by IL Manipulators?
        return LogHookAndMaybeRedirect(info, "modifier");
    }

    private static bool LogHookRemove(MethodBase from, Delegate to)
    {
        var info = GetHookInfo(from, to);

        return LogHookAndMaybeRedirect(info, "removed");
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

        internal Assembly Owner;

        internal MethodBase OriginalManaged;
        internal IntPtr OriginalNative;

        internal Delegate HookDelegate;
        internal IntPtr HookIntPtr;
        internal MethodBase HookMethodBase;
    }

    internal static bool RedirectFixFrameRateDependantLogicHooks = false;
    private static bool LogHookAndMaybeRedirect(HookInfo hookInfo, string context = "added")
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

        string GetToIdentifier()
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

        Log.Debug($"{hookInfo.Kind}Hook {GetToIdentifier()} {context} by assembly: {hookOwnerDllName} for: {fromIdentifier}");

        if (FixFrameRateDependantLogic.IsFixedEnabled &&
            RedirectFixFrameRateDependantLogicHooks &&
            hookInfo.HookDelegate != null && hookInfo.HookDelegate is ILContext.Manipulator manipulator)
        {
            // Redirect hooks to the right methods.
            // Not the right place for this or atleast it should be generalized but no time for this right now

            RedirectFixFrameRateDependantLogicHooks = false;

            try
            {
                void LogRedirection()
                {
                    Log.Info($"Redirecting {hookInfo.HookDelegate.Method?.DeclaringType?.FullName}.{hookInfo.HookDelegate.Method?.Name}" +
                        $" IL Hook to {fromIdentifier} because the {nameof(FixFrameRateDependantLogic)} fix is enabled.");
                }

                if (fromIdentifier == $"RoR2.{nameof(RoR2.PlayerCharacterMasterController)}.Update")
                {
                    LogRedirection();

                    new ILHook(
                        typeof(FixFrameRateDependantLogic).
                            GetMethod(nameof(FixFrameRateDependantLogic.PlayerCharacterMasterController_Update),
                            ReflectionHelper.AllFlags),
                        manipulator
                    );
                }
                else if (fromIdentifier == $"RoR2.{nameof(RoR2.PlayerCharacterMasterController)}.FixedUpdate")
                {
                    LogRedirection();

                    new ILHook(
                        typeof(FixFrameRateDependantLogic).
                            GetMethod(nameof(FixFrameRateDependantLogic.PlayerCharacterMasterController_FixedUpdate),
                            ReflectionHelper.AllFlags),
                        manipulator
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warning(e);
            }

            RedirectFixFrameRateDependantLogicHooks = true;
        }

        return true;
    }
}
