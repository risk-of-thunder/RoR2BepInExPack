using System;
using System.IO;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

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

    private static void LogOnHook(Assembly hookOwner, MethodBase originalMethod, MethodBase _, object __)
        => LogMethod(originalMethod, hookOwner);

    private static void LogILHook(Assembly hookOwner, MethodBase originalMethod, ILContext.Manipulator _)
        => LogMethod(originalMethod, hookOwner);

    private static void LogDetour(Assembly hookOwner, MethodBase originalMethod, MethodBase _)
        => LogMethod(originalMethod, hookOwner);

    private static void LogNativeDetour(Assembly hookOwner, MethodBase originalMethod, IntPtr _, IntPtr __)
        => LogMethod(originalMethod, hookOwner);

    private static bool LogHookAdd(MethodBase originalMethod, Delegate @delegate)
        => LogMethod(originalMethod, @delegate.Method.Module.Assembly);

    private static bool LogHookModify(MethodBase originalMethod, Delegate @delegate)
        => LogMethod(originalMethod, @delegate.Method.Module.Assembly);

    private static bool LogHookRemove(MethodBase originalMethod, Delegate @delegate)
        => LogMethod(originalMethod, @delegate.Method.Module.Assembly, false);

    private static bool LogMethod(MemberInfo originalMethod, Assembly hookOwnerAssembly, bool added = true)
    {
        if (originalMethod == null)
        {
            return true;
        }

        var hookOwnerDllName = "Not Found";
        if (hookOwnerAssembly != null)
        {
            // Get the dll name instead of assembly manifest name as this one one could be not correctly set by mod maker.
            hookOwnerDllName = Path.GetFileName(hookOwnerAssembly.Location);
        }

        var declaringType = originalMethod.DeclaringType;
        var name = originalMethod.Name;
        var identifier = declaringType != null ? $"{declaringType}.{name}" : name;

        Log.Debug($"Hook {(added ? "added" : "removed")} by assembly: {hookOwnerDllName} for: {identifier}");
        return true;
    }
}
