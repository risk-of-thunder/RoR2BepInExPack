using BepInEx;
using RoR2;
using RoR2BepInExPack.LegacyAssetSystem;
using RoR2BepInExPack.VanillaFixes;

namespace RoR2BepInExPack;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class RoR2BepInExPack : BaseUnityPlugin
{
    public const string PluginGUID = "___riskofthunder" + "." + PluginName;
    public const string PluginName = "RoR2BepInExPack";
    public const string PluginVersion = "1.0.3";

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

        LegacyResourcesDetours.Init();
        LegacyShaderDetours.Init();
    }

    private void OnEnable()
    {
        ILLine.Enable();
        SaferAchievementManager.Enable();
        SaferSearchableAttribute.Enable();
        FixConsoleLog.Enable();

        LegacyResourcesDetours.Enable();
        LegacyShaderDetours.Enable();
    }

    private void OnDisable()
    {
        LegacyShaderDetours.Disable();
        LegacyResourcesDetours.Disable();

        FixConsoleLog.Disable();
        SaferSearchableAttribute.Disable();
        SaferAchievementManager.Disable();
        ILLine.Disable();
    }

    private void OnDestroy()
    {
        LegacyShaderDetours.Destroy();
        LegacyResourcesDetours.Destroy();

        FixConsoleLog.Destroy();
        SaferSearchableAttribute.Destroy();
        SaferAchievementManager.Destroy();
        ILLine.Destroy();
    }
}
