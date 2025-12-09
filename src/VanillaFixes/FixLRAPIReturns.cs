using System;
using System.IO;
using BepInEx;
using UnityEngine;
using Path = System.IO.Path;

namespace RoR2BepInExPack.VanillaFixes;

internal class FixLRAPIReturns
{
    internal static void Init(PluginInfo pluginInfo)
    {
        try
        {
            const string lrapiJsonFileName = "lrapi_returns.json";

            var destination = Path.Combine(Application.streamingAssetsPath, lrapiJsonFileName);
            if (File.Exists(destination))
            {
                Log.Info($"{lrapiJsonFileName} already exists in StreamingAssets, skipping fix.");
                return;
            }

            var source = Path.Combine(Directory.GetParent(pluginInfo.Location).FullName, lrapiJsonFileName);

            File.Copy(source, destination);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }
}
