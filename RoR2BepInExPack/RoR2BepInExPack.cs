using BepInEx;

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

        LegacyResourcesDetours.Init();
    }

    private void OnEnable()
    {
        LegacyResourcesDetours.Enable();
    }

    private void OnDisable()
    {
        LegacyResourcesDetours.Disable();
    }

    private void OnDestroy()
    {
        LegacyResourcesDetours.Destroy();
    }
}
