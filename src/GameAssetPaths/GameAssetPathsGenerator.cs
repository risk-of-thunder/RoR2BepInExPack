#if GENERATE_GAME_ASSET_PATHS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace RoR2BepInExPack;

internal static class GameAssetPathsGenerator
{
    internal static void Init()
    {
        var outputPath = "GameAssetPaths.cs";
        var guidRegex = new Regex("^[0-9a-f]{32}$", RegexOptions.Compiled);

        var locator = Addressables.ResourceLocators.FirstOrDefault(l => l.LocatorId == "AddressablesMainContentCatalog") as ResourceLocationMap;
        if (locator == null)
        {
            Log.Error("Couldn't find game's locator");
            return;
        }

        var namespaceToClass = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<Type>>>>>();
        Dictionary<string, string> guidToPath = new Dictionary<string, string>();

        foreach (var (key, locations) in locator.Locations)
        {
            //There are at least 3 different key formats in the locator, filtering for guids
            if (key is not string guid || guid.Length != 32 || !guidRegex.IsMatch(guid))
            {
                continue;
            }

            foreach (var location in locations)
            {
                static void Add(
                    string primaryKey, string guid, Type type,
                    Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<Type>>>>> namespaceToClass,
                    Dictionary<string, string> guidToPath
                )
                {
                    var parts = primaryKey.SplitOnceFromLastIndexOf('/')
                        .Select(SanitizeForCSharp)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();
                    var className = parts[0];
                    var variableName = parts.Count > 1 ? parts[1] : "Asset";

                    className = DontUseCSharpKeywords(className);
                    variableName = DontUseCSharpKeywords(variableName);

                    var fullNamespace = "RoR2BepInExPack.GameAssetPaths.Version_" + RoR2BepInExPack.PluginVersion.Replace(".", "_");
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
                    if (!typesList.Contains(type))
                        typesList.Add(type);

                    guidToPath[guid] = primaryKey;
                }

                Add(location.PrimaryKey, guid, location.ResourceType, namespaceToClass, guidToPath);

                if (typeof(UnityEngine.Object).IsAssignableFrom(location.ResourceType) &&
                    location.HasDependencies)
                {
                    IResourceLocation assetBundleLocation = location.Dependencies.FirstOrDefault();
                    if (assetBundleLocation != null && assetBundleLocation.ResourceType == typeof(IAssetBundleResource))
                    {
                        AssetBundle sourceAssetBundle = Addressables.LoadAssetAsync<IAssetBundleResource>(assetBundleLocation).
                            WaitForCompletion().GetAssetBundle();
                        if (sourceAssetBundle)
                        {
                            foreach (var subAsset in 
                                    sourceAssetBundle.LoadAssetWithSubAssets(location.InternalId).Skip(1) /*skip first because first is main asset */)
                            {
                                string subAssetName = subAsset.name;
                                var subAssetKey = location.PrimaryKey + "[" + subAssetName + "]";
                                var subAssetGuid = guid + "[" + subAssetName + "]";

                                Add(subAssetKey, subAssetGuid, subAsset.GetType(), namespaceToClass, guidToPath);
                            }
                        }
                    }
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("#pragma warning disable CS1591");
        foreach (var (ns, classData) in namespaceToClass.OrderBy(e => e.Key))
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            foreach (var (className, variableData) in classData.OrderBy(e => e.Key))
            {
                sb.AppendLine($"    public static class {className}");
                sb.AppendLine("    {");
                foreach (var (variable, assets) in variableData.OrderBy(e => e.Key))
                {
                    if (assets.Count > 1)
                    {
                        foreach (var asset in assets.OrderBy(e => e.Key))
                        {
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

        var binaryOutputPath = Path.ChangeExtension(outputPath, ".bin");
        GameAssetPathsSerde.Serialize(binaryOutputPath, guidToPath.Values.ToArray(), guidToPath.Keys.ToArray());
        Log.Error($"Binary file generated at {Path.GetFullPath(binaryOutputPath)}");

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
