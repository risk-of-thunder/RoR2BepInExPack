using BepInEx;
using RoR2;
using RoR2BepInExPack.LegacyAssetSystem;
using RoR2BepInExPack.VanillaFixes;
using RoR2BepInExPack.ModCompatibility;
using RoR2BepInExPack.ReflectionHooks;

namespace RoR2BepInExPack;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class RoR2BepInExPack : BaseUnityPlugin
{
    public const string PluginGUID = "___riskofthunder" + "." + PluginName;
    public const string PluginName = "RoR2BepInExPack";
    public const string PluginVersion = "1.3.0";

    internal static PluginInfo pluginInfo;

    private void Awake()
    {
        pluginInfo = Info;
        Log.Init(Logger);

        RoR2Application.isModded = true;

        InitHooks();
    }

    private void InitHooks()
    {
        ILLine.Init();
        AutoCatchReflectionTypeLoadException.Init();
        SaferAchievementManager.Init();
        SaferSearchableAttribute.Init();
        FixConsoleLog.Init();
        FixConVar.Init();
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
        AutoCatchReflectionTypeLoadException.Enable();
        SaferAchievementManager.Enable();
        SaferSearchableAttribute.Enable();
        FixConsoleLog.Enable();
        FixConVar.Enable();
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
        FixConVar.Disable();
        SaferSearchableAttribute.Disable();
        SaferAchievementManager.Disable();
        AutoCatchReflectionTypeLoadException.Disable();
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
        FixConVar.Destroy();
        SaferSearchableAttribute.Destroy();
        SaferAchievementManager.Destroy();
        AutoCatchReflectionTypeLoadException.Destroy();
        ILLine.Destroy();
    }
}
