using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// Gearbox fucked up system initializer by making it look only at a Type[] in the ror2application behaviour.
// Simple fix really, just analyze everything and add it to that array.
internal class FixSystemInitializer
{
    private static Hook _hook = null;

    internal static void Init()
    {
        // We need this to call apply as soon as its created.
        _hook = new Hook(typeof(SystemInitializerAttribute).GetMethod(nameof(SystemInitializerAttribute.ExecuteStatic), ReflectionHelper.AllFlags), typeof(FixSystemInitializer).GetMethod(nameof(Unshittify), ReflectionHelper.AllFlags));
    }

    // Do not call orig.
    private static void Unshittify(Action _)
    {
        var instances = HG.Reflection.SearchableAttribute.GetInstances<SystemInitializerAttribute>();
        foreach (var instance in instances)
        {
            var target = (MethodInfo)instance.target;
            if (!target.IsStatic)
            {
                //Gearbox fucking sucks
                continue;
            }

            var casted = (SystemInitializerAttribute)instance;
            casted.methodInfo = target;
            casted.associatedType = target.DeclaringType;

            SystemInitializerAttribute.initializerAttributes.Enqueue(casted);
        }
    }
}
