using System;
using System.Collections;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// The EntityStateCatalog yields too often when applying entity state configurations which slows down loading
// Fix: wrap the original coroutine & block the majority of yield attempts
// Originally from RiskOfWaiting
internal class FixEntityStateCatalog
{
    private static Hook _hook;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _hook = new Hook(
                        typeof(EntityStateCatalog).GetMethod(nameof(EntityStateCatalog.SetElements), ReflectionHelper.AllFlags),
                        typeof(FixEntityStateCatalog).GetMethod(nameof(SetElementsLessYielding), ReflectionHelper.AllFlags),
                        hookConfig
                    );
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

    private static IEnumerator SetElementsLessYielding(Func<Type[], EntityStateConfiguration[], IEnumerator> orig, Type[] newEntityStateTypes, EntityStateConfiguration[] newEntityStateConfigurations)
    {
        IEnumerator result = orig(newEntityStateTypes, newEntityStateConfigurations);
        // yield one or two times to maintain a consistent framerate
        // this is the equivalent of yielding after every 800 configurations are applied, since the original coroutine already yields 1 in 10
        const int MAX_YIELD_ATTEMPTS_PER_FRAME = 80;
        int yieldAttemptsThisFrame = 0;
        while (result.MoveNext())
        {
            if (++yieldAttemptsThisFrame > MAX_YIELD_ATTEMPTS_PER_FRAME)
            {
                yieldAttemptsThisFrame = 0;
                yield return null;
            }
        }
    }
}
