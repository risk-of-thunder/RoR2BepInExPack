using BepInEx;
using RoR2;
using RoR2BepInExPack.LegacyAssetSystem;
using RoR2BepInExPack.VanillaFixes;
using RoR2BepInExPack.ModCompatibility;

namespace RoR2BepInExPack;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class RoR2BepInExPack : BaseUnityPlugin
{
    public const string PluginGUID = "___riskofthunder" + "." + PluginName;
    public const string PluginName = "RoR2BepInExPack";
    public const string PluginVersion = "1.2.0";

    private void Awake()
    {
        Log.Init(Logger);

        RoR2Application.isModded = true;

        InitHooks();
    }

    private void InitHooks()
    {
        ILLine.Init();
        SaferAchievementManager.Init();
        SaferSearchableAttribute.Init();
        FixConsoleLog.Init();
        FixDeathAnimLog.Init();
        FixNullBone.Init();
        FixExtraGameModesMenu.Init();

        LegacyResourcesDetours.Init();
        LegacyShaderDetours.Init();

        FixMultiCorrupt.Init(Config);
    }

    private void OnEnable()
    {
        ILLine.Enable();
        SaferAchievementManager.Enable();
        SaferSearchableAttribute.Enable();
        FixConsoleLog.Enable();
        FixDeathAnimLog.Enable();
        FixNullBone.Enable();
        FixExtraGameModesMenu.Enable();

        LegacyResourcesDetours.Enable();
        LegacyShaderDetours.Enable();

        FixMultiCorrupt.Enable();
    }

    private void OnDisable()
    {
        FixMultiCorrupt.Disable();

        LegacyShaderDetours.Disable();
        LegacyResourcesDetours.Disable();

        FixExtraGameModesMenu.Disable();
        FixNullBone.Disable();
        FixDeathAnimLog.Disable();
        FixConsoleLog.Disable();
        SaferSearchableAttribute.Disable();
        SaferAchievementManager.Disable();
        ILLine.Disable();
    }

    private void OnDestroy()
    {
        FixMultiCorrupt.Destroy();

        LegacyShaderDetours.Destroy();
        LegacyResourcesDetours.Destroy();

        FixExtraGameModesMenu.Destroy();
        FixNullBone.Destroy();
        FixDeathAnimLog.Destroy();
        FixConsoleLog.Destroy();
        SaferSearchableAttribute.Destroy();
        SaferAchievementManager.Destroy();
        ILLine.Destroy();
    }
}
