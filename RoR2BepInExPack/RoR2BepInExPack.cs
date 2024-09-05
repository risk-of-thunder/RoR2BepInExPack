using System;
using BepInEx;
using RoR2;
using RoR2BepInExPack.LegacyAssetSystem;
using RoR2BepInExPack.ModCompatibility;
using RoR2BepInExPack.ReflectionHooks;
using RoR2BepInExPack.UnityEngineHooks;
using RoR2BepInExPack.VanillaFixes;
using UnityEngine;

namespace RoR2BepInExPack;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class RoR2BepInExPack : BaseUnityPlugin
{
    public const string PluginGUID = "___riskofthunder" + "." + PluginName;
    public const string PluginName = "RoR2BepInExPack";
    public const string PluginVersion = "1.23.0";

    private void Awake()
    {
        Log.Init(Logger);

        RoR2Application.isModded = true;

        HookWatcher.Init();

        FrankenMonoPrintStackOverflowException.Init();

        InitHooks();

        Application.quitting += OnApplicationQuitting;
    }

    private void OnEnable()
    {
        EnableHooks();
    }

    private void OnDisable()
    {
        DisableHooks();
    }

    private void OnDestroy()
    {
        DestroyHooks();

        HookWatcher.Destroy();
    }

    private void InitHooks()
    {
        ILLine.Init();
        AutoCatchReflectionTypeLoadException.Init();
        SaferAchievementManager.Init();
        SaferResourceAvailability.Init();
        SaferSearchableAttribute.Init();
        FixConsoleLog.Init();
        FixConVar.Init();
        FixDeathAnimLog.Init();
        FixNullBone.Init();
        FixExtraGameModesMenu.Init();
        FixProjectileCatalogLimitError.Init();
        SaferWWise.Init();
        FixNullEntitlement.Init();
        FixExposeLog.Init();
        FixNonLethalOneHP.Init();
        FixRunScaling.Init();
        FixCharacterBodyRemoveOldestTimedBuff.Init();
        FixDedicatedServerMaxPlayerCount.Init();
        FixHasEffectiveAuthority.Init();
        FixSystemInitializer.Init();
        FixFrameRateDependantLogic.Init(Config);

        LegacyResourcesDetours.Init();
        LegacyShaderDetours.Init();

        FixMultiCorrupt.Init(Config);
    }

    private static void EnableHooks()
    {
        ILLine.Enable();
        AutoCatchReflectionTypeLoadException.Enable();
        SaferAchievementManager.Enable();
        SaferResourceAvailability.Enable();
        SaferSearchableAttribute.Enable();
        FixConsoleLog.Enable();
        FixConVar.Enable();
        FixDeathAnimLog.Enable();
        FixNullBone.Enable();
        FixExtraGameModesMenu.Enable();
        FixProjectileCatalogLimitError.Enable();
        SaferWWise.Enable();
        FixNullEntitlement.Enable();
        FixExposeLog.Enable();
        FixNonLethalOneHP.Enable();
        FixRunScaling.Enable();
        FixCharacterBodyRemoveOldestTimedBuff.Enable();
        FixDedicatedServerMaxPlayerCount.Enable();
        FixHasEffectiveAuthority.Enable();

        LegacyResourcesDetours.Enable();
        LegacyShaderDetours.Enable();

        FixMultiCorrupt.Enable();
    }

    private static void DisableHooks()
    {
        FixMultiCorrupt.Disable();

        LegacyShaderDetours.Disable();
        LegacyResourcesDetours.Disable();

        FixHasEffectiveAuthority.Disable();
        FixDedicatedServerMaxPlayerCount.Disable();
        FixCharacterBodyRemoveOldestTimedBuff.Disable();
        FixRunScaling.Disable();
        FixNonLethalOneHP.Disable();
        FixExposeLog.Disable();
        FixNullEntitlement.Disable();
        SaferWWise.Disable();
        FixProjectileCatalogLimitError.Disable();
        FixExtraGameModesMenu.Disable();
        FixNullBone.Disable();
        FixDeathAnimLog.Disable();
        FixConsoleLog.Disable();
        FixConVar.Disable();
        SaferSearchableAttribute.Disable();
        SaferResourceAvailability.Disable();
        SaferAchievementManager.Disable();
        AutoCatchReflectionTypeLoadException.Disable();
        ILLine.Disable();
    }

    private static void DestroyHooks()
    {
        FixMultiCorrupt.Destroy();

        LegacyShaderDetours.Destroy();
        LegacyResourcesDetours.Destroy();

        FixHasEffectiveAuthority.Destroy();
        FixDedicatedServerMaxPlayerCount.Destroy();
        FixCharacterBodyRemoveOldestTimedBuff.Destroy();
        FixRunScaling.Destroy();
        FixNonLethalOneHP.Destroy();
        FixExposeLog.Destroy();
        FixNullEntitlement.Destroy();
        SaferWWise.Destroy();
        FixProjectileCatalogLimitError.Destroy();
        FixExtraGameModesMenu.Destroy();
        FixNullBone.Destroy();
        FixDeathAnimLog.Destroy();
        FixConsoleLog.Destroy();
        FixConVar.Destroy();
        SaferSearchableAttribute.Destroy();
        SaferResourceAvailability.Destroy();
        SaferAchievementManager.Destroy();
        AutoCatchReflectionTypeLoadException.Destroy();
        ILLine.Destroy();
    }

    private void OnApplicationQuitting()
    {
        // Some mods add prefabs loaded from asset bundles directly as children
        // to runtime prefabs instantiated with PrefabsAPI.InstantiateClone (or by similar means).
        // And it seems like these prefabs are destroyed before runtime prefabs when the game is closing,
        // which results in a harmless, but certainly annoying crash.
        // Destroying all DontDestroyOnLoad objects before the game does seems to work out,
        // but we have to use DestroyImmediate because at this point
        // normal Destroy doesn't work
        var tmp = new GameObject();
        DontDestroyOnLoad(tmp);
        foreach (var obj in tmp.scene.GetRootGameObjects())
        {
            try
            {
                DestroyImmediate(obj);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
