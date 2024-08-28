using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

//Gearbox fucked up system initializer by making it look only at a Type[] in the ror2application behaviour.
//Simple fix really, just analyze everything and add it to that array.
internal class FixSystemInitializer
{
    private static Hook _hook = null;

    internal static void Init()
    {
        //We need this to call apply as soon as its created.
        _hook = new Hook(typeof(SystemInitializerAttribute).GetMethod(nameof(SystemInitializerAttribute.ExecuteStatic), ReflectionHelper.AllFlags), typeof(FixSystemInitializer).GetMethod(nameof(Unshittify), ReflectionHelper.AllFlags));
    }

    //Do not call orig.
    private static void Unshittify(Action _)
    {
        var instances = HG.Reflection.SearchableAttribute.GetInstances<SystemInitializerAttribute>();
        foreach(var instance in instances)
        {
            var target = (MethodInfo)instance.target;
            if(!target.IsStatic)
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
    /*private static ILHook _ilHook = null;
    private static HookConfig _hookConfig;
    
    /*internal static void Init()
    {
        //Dont manual apply, we need to run this before ror2's awake call starts.
        _ilHook = new ILHook(
            typeof(RoR2.RoR2Application).GetMethod(nameof(RoR2.RoR2Application.Awake), ReflectionHelper.AllFlags),
            Fix);
    }

    private static void Fix(ILContext context)
    {
        var cursor = new ILCursor(context);

        //Should put us right before the check to see if the assembly types have been obtained
        bool match = cursor.TryGotoNext(MoveType.After, x => x.MatchLdstr("buildId = "),
            x => x.MatchLdsfld<RoR2Application>(nameof(RoR2.RoR2Application.buildId)),
            x => x.MatchCallOrCallvirt<string>(nameof(string.Concat)),
            x => x.MatchCallOrCallvirt<RoR2Application>(nameof(RoR2Application.Print)));

        if (match)
        {
            cursor.EmitDelegate(() =>
            {
                RoR2Application.AssemblyTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
            });
        }
    }*/
}
