using System;
using System.Diagnostics;
using System.Linq;
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
    public const string PluginVersion = "1.15.0";

    private void Awake()
    {
        Log.Init(Logger);

        RoR2Application.isModded = true;

        HookWatcher.Init();

        FrankenMonoPrintStackOverflowException.Init();

        InitHooks();

        InitNativeHooks();
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
        SaferSearchableAttribute.Init();
        SaferResourceAvailability.Init();
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

        LegacyResourcesDetours.Init();
        LegacyShaderDetours.Init();

        FixMultiCorrupt.Init(Config);
    }

    private static void EnableHooks()
    {
        ILLine.Enable();
        AutoCatchReflectionTypeLoadException.Enable();
        SaferAchievementManager.Enable();
        SaferSearchableAttribute.Enable();
        SaferResourceAvailability.Enable();
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
        SaferResourceAvailability.Disable();
        SaferSearchableAttribute.Disable();
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
        SaferResourceAvailability.Destroy();
        SaferSearchableAttribute.Destroy();
        SaferAchievementManager.Destroy();
        AutoCatchReflectionTypeLoadException.Destroy();
        ILLine.Destroy();
    }

    private void InitNativeHooks()
    {
        var proc = Process.GetCurrentProcess().Modules
            .Cast<ProcessModule>()
            .FirstOrDefault(IsUnityPlayer) ?? Process.GetCurrentProcess().MainModule;
        var baseAddress = proc.BaseAddress;

        Application.quitting += () =>
        {
            PreDestroyRecursiveNullCheck.Init(baseAddress);
            DestroyGameObjectRecursiveNullCheck.Init(baseAddress);
        };

        static bool IsUnityPlayer(ProcessModule p)
        {
            return p.ModuleName.ToLowerInvariant().Contains("unityplayer");
        }
    }
}
