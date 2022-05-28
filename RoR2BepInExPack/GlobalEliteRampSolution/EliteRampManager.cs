using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RoR2BepInExPack.GlobalEliteRampSolution;

public static class EliteRampManager
{
    internal static List<(EliteDef, Texture2D)> elitesAndRamps = new();
    internal static Dictionary<EliteIndex, Texture2D> eliteIndexToTexture = new();
    private static ILHook ilHook;
    private static Texture2D vanillaEliteRamp;
    private static int EliteRampPropertyID => Shader.PropertyToID("_EliteRamp");
    public static void AddRamp(EliteDef def, Texture2D ramp)
    {
        try
        {
            if (def.shaderEliteRampIndex > 0) //An index of -1 (which is the default one) or lower causes no color remap to occur.
                def.shaderEliteRampIndex = 0;

            elitesAndRamps.Add((def, ramp));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static void ILUpdateRampProperly(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        var firstMatchSuccesful = c.TryGotoNext(MoveType.After,
                                    x => x.MatchLdarg(0),
                                    x => x.MatchLdfld<CharacterModel>(nameof(CharacterModel.propertyStorage)),
                                    x => x.MatchLdsfld(typeof(CommonShaderProperties), nameof(CommonShaderProperties._EliteIndex)));

        var secondMatchSuccesful = c.TryGotoNext(MoveType.After,
                                     x => x.MatchCallOrCallvirt<MaterialPropertyBlock>(nameof(MaterialPropertyBlock.SetFloat)));

        if(firstMatchSuccesful && secondMatchSuccesful)
        {
            c.Emit(OpCodes.Ldarg, 0);
            c.EmitDelegate(UpdateRampProperly);
        }
        else
        {
            Log.Error($"Elite Ramp ILHook failed");
        }
    }

    private static void UpdateRampProperly(CharacterModel charModel)
    {
        if(charModel.myEliteIndex != EliteIndex.None && eliteIndexToTexture.TryGetValue(charModel.myEliteIndex, out var ramp))
        {
            charModel.propertyStorage.SetTexture(EliteRampPropertyID, ramp);
            return;
        }
        charModel.propertyStorage.SetTexture(EliteRampPropertyID, vanillaEliteRamp);
    }

    private static void SetupDictionary()
    {
        Log.Debug($"Setting up dictionary");
        foreach(var tuple in elitesAndRamps)
        {
            var def = tuple.Item1;
            var ramp = tuple.Item2;

            eliteIndexToTexture[def.eliteIndex] = ramp;
            Log.Debug($"Tying index {def.eliteIndex} ({def}) to {ramp})");
        }
        elitesAndRamps.Clear();
    }
    #region Init,Enable,Disable,Destroy
    internal static async void Init()
    {
        try
        {
            RoR2Application.onLoad += SetupDictionary;
        
            var hookConfig = new HookConfig() { ManualApply = true };
            ilHook = new ILHook(typeof(CharacterModel).GetMethod(nameof(CharacterModel.UpdateMaterials), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), ILUpdateRampProperly);

            vanillaEliteRamp = await Addressables.LoadAssetAsync<Texture2D>("RoR2/Base/Common/ColorRamps/texRampElites.psd").Task;
        }
        catch (Exception ex)
        {
            Log.Error($"{nameof(EliteRampManager)} failed to initialize: {ex}");
        }
    }

    internal static void Enable()
    {
        ilHook.Apply();
    }

    internal static void Disable()
    {
        ilHook.Undo();
    }

    internal static void Destroy()
    {
        ilHook.Free();
    }
    #endregion
}
