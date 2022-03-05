using BepInEx;
using RoR2;
using RoR2BepInExPack.LegacyAssetSystem;
using RoR2BepInExPack.VanillaFixes;

namespace RoR2BepInExPack;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class RoR2BepInExPack : BaseUnityPlugin
{
    public const string PluginGUID = "riskofthunder";
    public const string PluginName = "RoR2BepInExPack";
    public const string PluginVersion = "1.0.0";

    private void Awake()
    {
        Log.Init(Logger);

        RoR2Application.isModded = true;

        InitHooks();
    }

    private void InitHooks()
    {
        FixConsoleLog.Init();
        FixEclipseButton.Init();

        LegacyResourcesDetours.Init();
        LegacyShaderDetours.Init();
    }

    private void OnEnable()
    {
        FixConsoleLog.Enable();
        FixEclipseButton.Enable();

        LegacyResourcesDetours.Enable();
        LegacyShaderDetours.Enable();
    }

    private void OnDisable()
    {
        LegacyShaderDetours.Disable();
        LegacyResourcesDetours.Disable();

        FixEclipseButton.Disable();
        FixConsoleLog.Disable();
    }

    private void OnDestroy()
    {
        LegacyShaderDetours.Destroy();
        LegacyResourcesDetours.Destroy();

        FixEclipseButton.Destroy();
        FixConsoleLog.Destroy();
    }
}
