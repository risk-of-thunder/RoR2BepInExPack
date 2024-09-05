using System;
using System.Linq;
using System.Reflection;
using HG.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// SearchableAttribute cctor can fail pretty hard since it only handle reflection exceptions outside of the for loop
// Fix : Make it so the exceptions are catched for each loop iteration
// Because its a cctor, we'll have to rerun it because it'll run before we have a chance to hook it
// Hopefully its only temporary and HG fixes it
// Note: the one in the RoR2.dll is a fake, its not actually used anywhere
internal class SaferSearchableAttribute
{
    private static ILHook _ilHook;
    private static Hook _deterministicInitTimingHook;

    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };

        _ilHook = new ILHook(
            typeof(SearchableAttribute).GetMethod(nameof(SearchableAttribute.ScanAssembly), ReflectionHelper.AllFlags),
            SaferScanAssemblyILManipulator,
            ilHookConfig
        );

        var hookConfig = new HookConfig() { ManualApply = true };

        _deterministicInitTimingHook = new Hook(
           typeof(RoR2.Console).GetMethod(nameof(RoR2.Console.Awake), ReflectionHelper.AllFlags),
           DeterministicInitTimingHook,
           hookConfig
       );
    }

    internal static void Enable()
    {
        _ilHook.Apply();
        _deterministicInitTimingHook.Apply();
    }

    internal static void Disable()
    {
        _deterministicInitTimingHook.Undo();
        _ilHook.Undo();
    }

    internal static void Destroy()
    {
        _deterministicInitTimingHook.Free();
        _ilHook.Free();
    }

    private static void DeterministicInitTimingHook(Action<RoR2.Console> orig, RoR2.Console self)
    {
        try
        {
            SearchableAttribute.Initialize();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        orig(self);
    }

    private static void SaferScanAssemblyILManipulator(ILContext il)
    {
        var c = new ILCursor(il);

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate(SaferScanAssembly);
        c.Emit(OpCodes.Ret);
    }

    private static void SaferScanAssembly(Assembly ass)
    {
        if (SearchableAttribute.assemblyBlacklist.Contains(ass.FullName))
        {
            return;
        }

        SearchableAttribute.assemblyBlacklist.Add(ass.FullName);

        if (ass.GetCustomAttribute<SearchableAttribute.OptInAttribute>() == null)
        {
            return;
        }

        var assTypes = ass.GetTypes();

        foreach (var type in assTypes)
        {
            var typeSearchableAttributes = Array.Empty<SearchableAttribute>();
            try
            {
                typeSearchableAttributes = type.
                    GetCustomAttributes(false).
                    Where(a => a is SearchableAttribute).
                    Cast<SearchableAttribute>().ToArray();
            }
            catch (Exception ex)
            {
                Log.Debug("ScanAssembly type.GetCustomAttributes(false) failed for :  " + type.FullName + Environment.NewLine + ex);
            }

            foreach (var attribute in typeSearchableAttributes)
            {
                try
                {
                    SearchableAttribute.RegisterAttribute(attribute, type);
                }
                catch (Exception ex)
                {
                    Log.Debug("SearchableAttribute.RegisterAttribute(attribute, type) failed for : " +
                        type.FullName +
                        Environment.NewLine +
                        ex);
                }
            }

            var memberInfos = Array.Empty<MemberInfo>();
            try
            {
                memberInfos = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch (Exception ex)
            {
                Log.Debug("type.GetMembers failed for : " +
                    type.FullName +
                    Environment.NewLine +
                    ex);
            }

            foreach (var memberInfo in memberInfos)
            {
                var memberInfoCustomAttributes = Array.Empty<SearchableAttribute>();
                try
                {
                    memberInfoCustomAttributes = memberInfo.
                        GetCustomAttributes(false).
                        Where(a => a is SearchableAttribute).
                        Cast<SearchableAttribute>().ToArray();
                }
                catch (Exception ex)
                {
                    Log.Debug("memberInfo.GetCustomAttributes(false) failed for : " +
                        type.FullName +
                        Environment.NewLine +
                        memberInfo.Name +
                        Environment.NewLine +
                        ex);
                }

                foreach (var attribute in memberInfoCustomAttributes)
                {
                    try
                    {
                        SearchableAttribute.RegisterAttribute(attribute, memberInfo);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("SearchableAttribute.RegisterAttribute(attribute, memberInfo) failed for : " +
                            type.FullName +
                            Environment.NewLine +
                            memberInfo.Name +
                            Environment.NewLine +
                            ex);
                    }
                }
            }
        }
    }
}
