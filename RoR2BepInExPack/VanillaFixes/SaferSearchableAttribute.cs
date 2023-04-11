using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HG.Reflection;
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
    private static Hook _saferCctorHook;

    private static Hook _deterministicCctorTimingHook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _saferCctorHook = new Hook(
                        typeof(SearchableAttribute).GetMethod(nameof(SearchableAttribute.ScanAllAssemblies), ReflectionHelper.AllFlags),
                        typeof(SaferSearchableAttribute).GetMethod(nameof(SaferSearchableAttribute.TryCatchEachLoopIteration), ReflectionHelper.AllFlags),
                        hookConfig
                    );

        _deterministicCctorTimingHook = new Hook(
                        typeof(RoR2.RoR2Application).GetMethod(nameof(RoR2.RoR2Application.OnLoad), ReflectionHelper.AllFlags),
                        typeof(SaferSearchableAttribute).GetMethod(nameof(SaferSearchableAttribute.DeterministicCctorTiming), ReflectionHelper.AllFlags),
                        hookConfig
                    );
    }

    internal static void Enable()
    {
        _saferCctorHook.Apply();
        _deterministicCctorTimingHook.Apply();
    }

    internal static void Disable()
    {
        _deterministicCctorTimingHook.Undo();
        _saferCctorHook.Undo();
    }

    internal static void Destroy()
    {
        _deterministicCctorTimingHook.Free();
        _saferCctorHook.Free();
    }

    private static void TryCatchEachLoopIteration(Action _)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                ScanAssembly(assembly);
            }
            catch (Exception ex)
            {
                Log.Debug("ScanAssembly failed for assembly :  " + assembly.FullName + Environment.NewLine + ex);
            }
        }
    }

    private static void ScanAssembly(Assembly assembly)
    {
        if (SearchableAttribute.assemblyBlacklist.Contains(assembly.FullName))
        {
            return;
        }

        SearchableAttribute.assemblyBlacklist.Add(assembly.FullName);

        if (assembly.GetCustomAttribute<SearchableAttribute.OptInAttribute>() == null)
        {
            return;
        }

        var assemblyTypes = assembly.GetTypes();

        foreach (var type in assemblyTypes)
        {
            var typeCustomAttributes = Array.Empty<SearchableAttribute>();
            try
            {
                typeCustomAttributes = type.GetCustomAttributes(false).Where(a => a is SearchableAttribute).Cast<SearchableAttribute>().ToArray();
            }
            catch (Exception ex)
            {
                Log.Debug("ScanAssembly type.GetCustomAttributes(false) failed for :  " + type.FullName + Environment.NewLine + ex);
            }

            foreach (var attribute in typeCustomAttributes)
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
                    memberInfoCustomAttributes = memberInfo.GetCustomAttributes(false).Where(a => a is SearchableAttribute).Cast<SearchableAttribute>().ToArray();
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

    private static IEnumerator DeterministicCctorTiming(Func<RoR2.RoR2Application, IEnumerator> orig, RoR2.RoR2Application self)
    {
        RunCctorAgain();

        return orig(self);
    }

    private static void RunCctorAgain()
    {
        typeof(SearchableAttribute).TypeInitializer.Invoke(null, null);
    }
}
