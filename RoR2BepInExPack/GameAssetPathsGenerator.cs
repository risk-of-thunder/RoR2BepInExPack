#if GENERATE_GAME_ASSET_PATHS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine.AddressableAssets;

namespace RoR2BepInExPack;

internal static class GameAssetPathsGenerator
{
    struct AssetVariableToGuid
    {
        public string Variable;
        public string Guid;
    }

    internal static void Init()
    {
        var jsonPath = Path.Combine(BepInEx.Paths.GameRootPath, "Risk of Rain 2_Data", "StreamingAssets", "lrapi_returns.json");
        string outputPath = "GameAssetPaths.cs";

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

        var namespaceToClass = new Dictionary<string, Dictionary<string, List<AssetVariableToGuid>>>();

        foreach (var kvp in assets)
        {
            var key = SanitizeKey(kvp.Key);
            var rawParts = Regex.Split(key, @"[^a-zA-Z0-9]+");
            var parts = rawParts
                .Select(SanitizeIdentifier)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count < 3)
            {
                Console.WriteLine($"Skipping malformed key: {kvp.Key}");
                continue;
            }

            // First part is the namespace
            // Class name is middle parts (everything except first and last 2)
            var classParts = parts.Take(parts.Count - 2).ToList();
            // Variable is last 2 parts joined
            var variableName = parts[^2] + "_" + parts[^1];

            // Handle single-part class name fallback
            var className = string.Join("_", classParts);

            // Sanitize identifiers against C# keywords
            className = DontUseCSharpKeywords(className);
            variableName = DontUseCSharpKeywords(variableName);

            var fullNamespace = "RoR2BepInExPack.GameAssetPaths";

            if (!namespaceToClass.TryGetValue(fullNamespace, out var classMap))
            {
                classMap = new Dictionary<string, List<AssetVariableToGuid>>();
                namespaceToClass[fullNamespace] = classMap;
            }

            if (!classMap.TryGetValue(className, out var assetList))
            {
                assetList = new List<AssetVariableToGuid>();
                classMap[className] = assetList;
            }

            assetList.Add(new AssetVariableToGuid
            {
                Variable = variableName,
                Guid = kvp.Value
            });
        }

        var sb = new StringBuilder();
        sb.AppendLine("#pragma warning disable CS1591");
        foreach (var (ns, classData) in namespaceToClass)
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            foreach (var (className, variableData) in classData)
            {
                sb.AppendLine($"    public static class {className}");
                sb.AppendLine("    {");
                foreach (var assetMetaData in variableData)
                {
                    try
                    {
                        var asset = Addressables.LoadAssetAsync<UnityObject>(assetMetaData.Guid).WaitForCompletion();
                        if (asset)
                        {
                            var assetType = asset.GetType().FullName;
                            sb.AppendLine($"    /// <summary>");
                            sb.AppendLine($"    /// {assetType}");
                            sb.AppendLine($"    /// </summary>");
                            sb.AppendLine($"    public static string {assetMetaData.Variable} = \"{assetMetaData.Guid}\";");
                        }
                        else
                        {
                            AppendLineAssetNoTypeFound(sb, assetMetaData.Variable, assetMetaData.Guid);
                        }
                    }
                    catch (Exception)
                    {
                        AppendLineAssetNoTypeFound(sb, assetMetaData.Variable, assetMetaData.Guid);
                    }
                }
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
        }

        sb.AppendLine("#pragma warning restore CS1591");

        File.WriteAllText(outputPath, sb.ToString());

        Log.Error($"C# class generated at {Path.GetFullPath(outputPath)}");
    }

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

    private static string DontUseCSharpKeywords(string str)
    {
        if (str == "params" || str == "ref" || str == "switch" || str == "base" || str == "default" || str == "void")
        {
            return str[0].ToString().ToUpper() + str.Substring(1);
        }

        return str;
    }

    static string SanitizeKey(string key)
    {
        string sanitized = Regex.Replace(key, @"[^a-zA-Z0-9_]", "_");
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        return sanitized;
    }

    static string SanitizeIdentifier(string input)
    {
        string clean = Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");
        return char.IsLetter(clean[0]) ? clean : "_" + clean;
    }
}
#endif
