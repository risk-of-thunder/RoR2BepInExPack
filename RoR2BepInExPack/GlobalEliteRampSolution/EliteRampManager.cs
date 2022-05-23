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
    internal static Dictionary<EliteDef, Texture2D> eliteToTexture = new();
    private static ILHook ilHook;
    private static Texture2D vanillaEliteRamp;
    private static int EliteRampPropertyID => Shader.PropertyToID("_EliteRamp");
    public static void AddRamp(EliteDef def, Texture2D ramp)
    {
        try
        {
            if (eliteToTexture.ContainsKey(def))
                throw new InvalidOperationException($"Cannot add EliteDef {def} as its already in the dictionary");

            eliteToTexture.Add(def, ramp);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
    private static void ILUpdateRampProperly(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        c.GotoNext(MoveType.After,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<CharacterModel>(nameof(CharacterModel.propertyStorage)),
            x => x.MatchLdsfld(typeof(CommonShaderProperties), nameof(CommonShaderProperties._EliteIndex)));

        c.GotoNext(MoveType.After,
            x => x.MatchCallOrCallvirt<MaterialPropertyBlock>(nameof(MaterialPropertyBlock.SetFloat)));

        c.Emit(OpCodes.Ldarg, 0);
        c.EmitDelegate(UpdateRampProperly);
    }

    private static void UpdateRampProperly(CharacterModel charModel)
    {
        EliteDef myEliteDef = EliteCatalog.GetEliteDef(charModel.myEliteIndex);
        if(eliteToTexture.TryGetValue(myEliteDef, out var ramp))
        {
            charModel.propertyStorage.SetTexture(EliteRampPropertyID, ramp);
            return;
        }

        charModel.propertyStorage.SetTexture(EliteRampPropertyID, vanillaEliteRamp);
    }

    #region Init,Enable,Disable,Destroy
    internal static async void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };
        ilHook = new ILHook(typeof(CharacterModel).GetMethod(nameof(CharacterModel.UpdateMaterials)), ILUpdateRampProperly);

        vanillaEliteRamp = await Addressables.LoadAssetAsync<Texture2D>("RoR2/Base/Common/ColorRamps/texRampElites.psd").Task;
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
