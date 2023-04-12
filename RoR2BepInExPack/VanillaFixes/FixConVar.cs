using System;
using System.Linq;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// Convars in vanilla only check for ConVars in the base RoR2 Assembly, which means ConVars cant be used
// by other assemblies
// Fix: Make it so all assemblies are scanned for ConVarss
internal static class FixConVar
{
    private static ILHook _ilHook;

    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig { ManualApply = true };
        _ilHook = new ILHook(typeof(RoR2.Console).GetMethod(nameof(RoR2.Console.InitConVars), ReflectionHelper.AllFlags),
            ScanAllAssemblies,
            ref ilHookConfig);
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

    private static void ScanAllAssemblies(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        bool ilFound = c.TryGotoNext(MoveType.After, x => x.MatchLdtoken(out _),
            x => x.MatchCallOrCallvirt<Type>(nameof(Type.GetTypeFromHandle)),
            x => x.MatchCallOrCallvirt<Type>("get_Assembly"),
            x => x.MatchCallOrCallvirt<Assembly>(nameof(Assembly.GetTypes)));

        if (ilFound)
        {
            c.EmitDelegate(LoadAllConVars);
        }
        else
        {
            Log.Error("FixConVar TryGotoNext failed, not applying patch");
        }
    }

    //We cant load r2api's types because EmbeddedResources causes mono to commit sudoku and die
    private static Type[] LoadAllConVars(Type[] dontCareTypes)
    {
        return AppDomain.CurrentDomain.GetAssemblies().Where(ass => !ass.FullName.Contains("R2API") && ass.GetCustomAttribute<HG.Reflection.SearchableAttribute.OptInAttribute>() != null).SelectMany(ass => ass.GetTypes()).ToArray();
    }
}
