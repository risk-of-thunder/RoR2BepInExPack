using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ConVar;
using RoR2BepInExPack.Reflection;
using UnityEngine;

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
        _ilHook = new ILHook(
            typeof(RoR2.Console).GetMethod(nameof(RoR2.Console.InitConVars), ReflectionHelper.AllFlags),
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

        c.Emit(OpCodes.Ldarg_0);

        c.EmitDelegate(LoadAllConVars);

        c.Emit(OpCodes.Ret);
    }

    private static bool IsMonoFriendlyType(this Type type)
    {
        const string DelegatePointerTypeName = "MonoFNPtrFakeClass";

        if (type.GetFields(ReflectionHelper.AllFlags).Any(fi => fi.FieldType.Name == DelegatePointerTypeName))
        {
            Log.Debug($"Not scanning {type} for ConVars due to it containing delegate pointer field(s)");
            return false;
        }

        return true;
    }

    private static void LoadAllConVars(RoR2.Console self)
    {
        self.allConVars = new();
        self.archiveConVars = new();

        var assTypes = new List<Type>();

        foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (ass.GetCustomAttribute<HG.Reflection.SearchableAttribute.OptInAttribute>() != null)
                {
                    assTypes.AddRange(ass.GetTypes().Where(t => t.IsMonoFriendlyType()));
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }

        foreach (var type in assTypes)
        {
            try
            {
                foreach (var fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        if (fieldInfo.FieldType.IsSubclassOf(typeof(BaseConVar)))
                        {
                            if (fieldInfo.IsStatic)
                            {
                                BaseConVar conVar = (BaseConVar)fieldInfo.GetValue(null);
                                self.RegisterConVarInternal(conVar);
                            }
                            else if (type.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                            {
                                Debug.LogError($"ConVar defined as {type.Name}.{fieldInfo.Name} could not be registered. ConVars must be static fields.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Debug(e);
                    }
                }

                foreach (var methodInfo in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        if (methodInfo.GetCustomAttribute<ConVarProviderAttribute>() != null)
                        {
                            if (methodInfo.ReturnType != typeof(IEnumerable<BaseConVar>) ||
                                methodInfo.GetParameters().Length != 0)
                            {
                                Debug.LogError("ConVar provider {type.Name}.{methodInfo.Name} does not match the signature \"static IEnumerable<ConVar.BaseConVar>()\".");
                            }
                            else if (!methodInfo.IsStatic)
                            {
                                Debug.LogError($"ConVar provider {type.Name}.{methodInfo.Name} could not be invoked. Methods marked with the ConVarProvider attribute must be static.");
                            }
                            else
                            {
                                foreach (var convar in (IEnumerable<BaseConVar>)methodInfo.Invoke(null, Array.Empty<object>()))
                                {
                                    self.RegisterConVarInternal(convar);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Debug(e);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }

        foreach (var value in self.allConVars.Values)
        {
            try
            {
                if ((value.flags & ConVarFlags.Engine) != ConVarFlags.None)
                {
                    value.defaultValue = value.GetString();
                }
                else if (value.defaultValue != null)
                {
                    value.AttemptSetString(value.defaultValue);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
