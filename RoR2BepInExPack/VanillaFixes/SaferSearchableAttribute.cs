using HG.Reflection;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;
using System;

namespace RoR2BepInExPack.VanillaFixes;

// SearchableAttribute cctor can fail pretty hard since it only handle reflection exceptions outside of the for loop
// Fix : Make it so the exceptions are catched for each loop iteration
// Because its a cctor, we'll have to rerun it because it'll run before we have a chance to hook it
// Hopefully its only temporary and HG fixes it
internal class SaferSearchableAttribute
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        _hook = new Hook(
                        typeof(SearchableAttribute).GetMethod(nameof(SearchableAttribute.ScanAllAssemblies), ReflectionHelper.AllFlags),
                        typeof(SaferSearchableAttribute).GetMethod(nameof(SaferSearchableAttribute.TryCatchEachLoopIteration), ReflectionHelper.AllFlags),
                        hookConfig
                    );
    }

    internal static void Enable()
    {
        _hook.Apply();

        RunCctorAgain();
    }

    private static void RunCctorAgain()
    {
        typeof(SearchableAttribute).TypeInitializer.Invoke(null, null);
    }

    internal static void Disable()
    {
        _hook.Undo();
    }

    internal static void Destroy()
    {
        _hook.Free();
    }

    private static void TryCatchEachLoopIteration(Action orig)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            try
            {
                SearchableAttribute.ScanAssembly(assemblies[i]);
            }
            catch (Exception ex)
            {
                Log.Warning("HG.Reflection.SearchableAttribute.ScanAssembly failed for assembly :  " + assemblies[i].FullName + Environment.NewLine + ex);
            }
        }
    }
}
