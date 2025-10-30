#if GENERATE_GAME_ASSET_PATHS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace RoR2BepInExPack;

internal static class GameAssetPathsGenerator
{
    internal static void Init()
    {
        var outputPath = "GameAssetPathsBetter.cs";
        var jsonPath = Path.Combine(BepInEx.Paths.GameRootPath, "Risk of Rain 2_Data", "StreamingAssets", "lrapi_returns.json");
        var guidRegex = new Regex("^[0-9a-f]{32}$", RegexOptions.Compiled);

        if (!File.Exists(jsonPath))
        {
            Log.Error("JSON file not found.");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);

        Dictionary<string, string> lrapiAssets;
        try
        {
            lrapiAssets = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent).ToDictionary(i => i.Value, i => i.Key);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to parse JSON: " + ex.Message);
            return;
        }

        if (lrapiAssets == null)
        {
            Log.Error("Failed to parse JSON.");
            return;
        }

        var locator = Addressables.m_Addressables.ResourceLocators.FirstOrDefault(l => l.LocatorId == "AddressablesMainContentCatalog") as ResourceLocationMap;
        if (locator == null)
        {
            Log.Error("Couldn't find game's locator");
            return;
        }

        var namespaceToClass = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<Type>>>>>();

        foreach (var (key, locations) in locator.Locations)
        {
            //There are at least 3 different key formats in the locator, filtering for guids
            if (key is not string guid || guid.Length != 32 || !guidRegex.IsMatch(guid))
            {
                continue;
            }

            foreach (var location in locations)
            {
                var parts = location.PrimaryKey.SplitOnceFromLastIndexOf('/')
                    .Select(SanitizeForCSharp)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                var className = parts[0];
                var variableName = parts.Count > 1 ? parts[1] : "Asset";

                className = DontUseCSharpKeywords(className);
                variableName = DontUseCSharpKeywords(variableName);

                var fullNamespace = "RoR2BepInExPack.GameAssetPathsBetter";
                if (!namespaceToClass.TryGetValue(fullNamespace, out var classMap))
                {
                    namespaceToClass[fullNamespace] = classMap = [];
                }

                if (!classMap.TryGetValue(className, out var variableMap))
                {
                    classMap[className] = variableMap = [];
                }

                if (!variableMap.TryGetValue(variableName, out var assetMap))
                {
                    variableMap[variableName] = assetMap = [];
                }

                if (!assetMap.TryGetValue(guid, out var typesList))
                {
                    assetMap[guid] = typesList = [];
                }
                typesList.Add(location.ResourceType);
            }
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
                foreach (var (variable, assets) in variableData.OrderBy(v => v.Key))
                {
                    if (assets.Count > 1)
                    {
                        foreach (var asset in assets)
                        {
                            //For backwards compatibility generate non-unique name for a guid in lrapi_returns.json
                            if (lrapiAssets.ContainsKey(asset.Key))
                            {
                                WriteField(string.Join(", ", asset.Value.Select(t => t.FullName)), variable, asset.Key);
                            }
                            WriteField(string.Join(", ", asset.Value.Select(t => t.FullName)), $"{variable}_{asset.Key[..8]}", asset.Key);
                        }
                    }
                    else
                    {
                        var asset = assets.First();
                        WriteField(string.Join(", ", asset.Value.Select(t => t.FullName)), variable, asset.Key);
                    }
                }
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
        }

        sb.AppendLine("#pragma warning restore CS1591");

        File.WriteAllText(outputPath, sb.ToString());

        Log.Error($"C# class generated at {Path.GetFullPath(outputPath)}");

        void WriteField(string types, string variable, string value)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// {types}");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public static string {variable} = \"{value}\";");
        }
    }

    private static string[] SplitOnceFromLastIndexOf(this string key, char separator)
    {
        int lastSep = key.LastIndexOf(separator);

        string[] parts;
        if (lastSep == -1)
        {
            parts = [key];
        }
        else
        {
            parts =
            [
                key.Substring(0, lastSep),
                    key.Substring(lastSep + 1)
            ];
        }

        return parts;
    }

    private static string DontUseCSharpKeywords(string str)
    {
        if (str == "params" || str == "ref" || str == "switch" || str == "base" || str == "default" || str == "void")
        {
            return str[0].ToString().ToUpper() + str.Substring(1);
        }

        return str;
    }

    static string SanitizeForCSharp(string input)
    {
        string s = Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");
        return char.IsLetter(s[0]) ? s : "_" + s;
    }
}
#endif
