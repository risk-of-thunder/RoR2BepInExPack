#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine.AddressableAssets;

namespace RoR2BepInExPack;

internal static class GameAssetPathsGenerator
{
    internal static void Init()
    {
        var jsonPath = Path.Combine(BepInEx.Paths.GameRootPath, "Risk of Rain 2_Data", "StreamingAssets", "lrapi_returns.json");
        string outputPath = "GameAssetPaths.cs";
        string className = "GameAssetPaths";

        if (!File.Exists(jsonPath))
        {
            Log.Error("JSON file not found.");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);

        Dictionary<string, string>? assets;
        try
        {
            assets = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to parse JSON: " + ex.Message);
            return;
        }

        if (assets == null)
        {
            Log.Error("Failed to parse JSON.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"namespace RoR2BepInExPack;\n");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Stores paths to game assets.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");

        foreach (var kvp in assets)
        {
            string key = SanitizeKey(kvp.Key);
            string value = kvp.Value;

            try
            {
                var asset = Addressables.LoadAssetAsync<UnityObject>(kvp.Value).WaitForCompletion();
                if (asset)
                {
                    var assetType = asset.GetType().FullName;
                    sb.AppendLine($"    /// <summary>");
                    sb.AppendLine($"    /// {assetType}");
                    sb.AppendLine($"    /// </summary>");
                    sb.AppendLine($"    public static string {key} = \"{value}\";");
                }
                else
                {
                    AppendLineAssetNoTypeFound(sb, key, value);
                }
            }
            catch (Exception)
            {
                AppendLineAssetNoTypeFound(sb, key, value);
            }
        }

        sb.AppendLine("}");

        File.WriteAllText(outputPath, sb.ToString());

        Log.Error($"C# class generated at {Path.GetFullPath(outputPath)}");

        static void AppendLineAssetNoTypeFound(StringBuilder sb, string key, string value)
        {
            if (key.EndsWith("_unity"))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Unity Scene");
                sb.AppendLine($"    /// </summary>");
            }
            else
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Asset loading failed");
                sb.AppendLine($"    /// </summary>");
            }

            sb.AppendLine($"    public static string {key} = \"{value}\";");
        }
    }

    static string SanitizeKey(string key)
    {
        // Replace invalid characters with underscores
        string sanitized = Regex.Replace(key, @"[^a-zA-Z0-9_]", "_");

        // Ensure it doesn't start with a digit
        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }
}
#endif
