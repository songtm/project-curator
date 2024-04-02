using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
// using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace Ogxd.ProjectCurator
{
    public static class ProjectCurator
    {
        public static string TpSheetPath = "Assets/XRes/FX/SpriteAtlas/";
        public static string TpSrcPath = "../TPSprites/sprites/";
        public static Dictionary<string, Dictionary<string, string>> TpSheetFileId2Name = new Dictionary<string, Dictionary<string, string>>();
        [NonSerialized]
        private static Dictionary<string, AssetInfo> pathToAssetInfo;

        static ProjectCurator()
        {
            pathToAssetInfo = new Dictionary<string, AssetInfo>();
            var assetInfos = ProjectCuratorData.AssetInfos;
            for (int i = 0; i < assetInfos.Length; i++) {
                pathToAssetInfo.Add(assetInfos[i].path, assetInfos[i]);
            }
        }

        public static  void BuildDepsGraph()
        {/*
            var asset2Bundle = BuildScriptPackedMode.AssetName2BundleName;
            var infos = new Dictionary<string, Dictionary<string, HashSet<string>>>();
            foreach (var pair in pathToAssetInfo)
            {
                var assetPath = pair.Key;
                if (asset2Bundle.TryGetValue(assetPath, out var srcBundle) && !srcBundle.Contains("LuaScripts"))
                {
                    if (!infos.TryGetValue(srcBundle, out var toDstBundlesRes))
                    {
                        toDstBundlesRes = new Dictionary<string, HashSet<string>>();
                        infos[srcBundle] = toDstBundlesRes;
                    }

                    var deps = pair.Value._dependencies;
                    foreach (var dependency in deps)
                    {
                        if (asset2Bundle.TryGetValue(dependency, out var dstBundle) && dstBundle != srcBundle)
                        {
                            if (!toDstBundlesRes.TryGetValue(dstBundle, out var reses))
                            {
                                reses =  new HashSet<string>();
                                toDstBundlesRes[dstBundle] = reses;
                            }

                            const int maxCount = 10;
                            if (reses.Count < maxCount)
                            {
                                reses.Add(dependency);
                                if (reses.Count == maxCount)
                                {
                                    reses.Add($"...skiped...");
                                }
                            }
                            
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            foreach (var pair in infos)
            {
                var srcBundle = pair.Key;
                // Debug.Log("");
                sb.AppendLine();
                // Debug.Log($"{srcBundle}:[{pair.Value.Count}]");
                sb.AppendLine($"{srcBundle}:[{pair.Value.Count}]");
                foreach (var toDstPair in pair.Value)
                {
                    var dstBundle = toDstPair.Key;
                    var res = toDstPair.Value;
                    // Debug.Log($"----{dstBundle}");
                    foreach (var re in res)
                    {
                        // Debug.Log($"--------{dstBundle}->{re}");
                        sb.AppendLine($"--------({dstBundle}) {re}");
                    }
                }
            }
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            File.WriteAllText(desktop + "/resDeps.txt", sb.ToString());
            
            // var sb = new StringBuilder();
            // sb.AppendLine("digraph graph_name{");
            // foreach (var pair in infos)
            // {
            //     foreach (var valuePair in pair.Value)
            //     {
            //         sb.AppendLine($"\"{pair.Key}\" -> \"{valuePair.Key}\";");
            //     }
            // }
            // sb.AppendLine("}");
            // string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // File.WriteAllText(desktop + "/test.dot", sb.ToString());
            */
        }
        
        public static AssetInfo GetAsset(string path)
        {
            AssetInfo assetInfo = null;
            pathToAssetInfo.TryGetValue(path, out assetInfo);
            return assetInfo;
        }

        public static void AddAssetToDatabase(string path, bool recursive = true, bool calcTpSheet = false)
        {
            AssetInfo assetInfo;
            if (!pathToAssetInfo.TryGetValue(path, out assetInfo)) {
                pathToAssetInfo.Add(path, assetInfo = new AssetInfo(path));
            }

            var dependencies = assetInfo.GetDependencies(recursive, calcTpSheet);

            foreach (string dependency in dependencies) {
                if (dependency == assetInfo.path)
                    continue;
                if (pathToAssetInfo.TryGetValue(dependency, out AssetInfo depInfo)) {
                    assetInfo.dependencies.Add(dependency);
                    depInfo.referencers.Add(assetInfo.path);
                    // Included status may have changed and need to be recomputed
                    depInfo.ClearIncludedStatus();
                }
            }
        }

        public static void RemoveAssetFromDatabase(string asset)
        {
            if (pathToAssetInfo.TryGetValue(asset, out AssetInfo assetInfo)) {
                foreach (string referencer in assetInfo.referencers) {
                    if (pathToAssetInfo.TryGetValue(referencer, out AssetInfo referencerAssetInfo)) {
                        if (referencerAssetInfo.dependencies.Remove(asset)) {
                            referencerAssetInfo.ClearIncludedStatus();
                        } else {
                            // Non-Reciprocity Error
                            Debug.LogWarning($"Asset '{referencer}' that depends on '{asset}' doesn't have it as a dependency");
                        }
                    } else {
                        Debug.LogWarning($"Asset '{referencer}' that depends on '{asset}' is not present in the database");
                    }
                }
                foreach (string dependency in assetInfo.dependencies) {
                    if (pathToAssetInfo.TryGetValue(dependency, out AssetInfo dependencyAssetInfo)) {
                        if (dependencyAssetInfo.referencers.Remove(asset)) {
                            dependencyAssetInfo.ClearIncludedStatus();
                        } else {
                            // Non-Reciprocity Error
                            Debug.LogWarning($"Asset '{dependency}' that is referenced by '{asset}' doesn't have it as a referencer");
                        }
                    } else {
                        Debug.LogWarning($"Asset '{dependency}' that is referenced by '{asset}' is not present in the database");
                    }
                }
                pathToAssetInfo.Remove(asset);
            } else {
                // Debug.LogWarning($"Asset '{asset}' is not present in the database"); //songtm disable warning for clean
            }
        }

        public static void ClearDatabase()
        {
            pathToAssetInfo.Clear();
        }

        public static void RebuildDatabase(bool recursive = true, bool calcTpSheet = false, bool includePackageDir = false)
        {
            pathToAssetInfo = new Dictionary<string, AssetInfo>();
            TpSheetFileId2Name.Clear();

            var allAssetPaths = AssetDatabase.GetAllAssetPaths();

            // Ignore non-assets (package folder for instance) and directories
            // add package folder back:songtm
            // allAssetPaths = allAssetPaths.Where(x => x.StartsWith("Assets/") || x.StartsWith("Packages/")  && !Directory.Exists(x)).ToArray();
            // songtm: AddressableAssetSettings引用了PrefabFX group, 这个group直接包含了技能特效prefab,这个prefab包含了shader,材质.... 太乱了
            allAssetPaths = allAssetPaths.Where(x =>
            {
                return !x.Contains("AddressableAssetsData") 
                       && (includePackageDir ? (x.StartsWith("Assets/") || x.StartsWith("Packages/")) : x.StartsWith("Assets/")) 
                       && !Directory.Exists(x);
            }).ToArray();

            EditorUtility.DisplayProgressBar("Building Dependency Database", "Gathering All Assets...", 0f);

            // Gather all assets
            for (int p = 0; p < allAssetPaths.Length; p++)
            {
                var path = allAssetPaths[p];
                if (calcTpSheet && path.EndsWith(".png") && path.StartsWith(TpSheetPath))
                {
                    TpSheetFileId2Name[path] = new Dictionary<string, string>();
                    var allText = File.ReadAllText(path + ".meta");
                    var matches = Regex.Matches(allText, @"- first:[\r\n]+.*: ([-\d]+)[\r\n]+.*second: ([^\r\n\r\n]*)");
                    foreach (Match match in matches)
                    {
                        var fileid = match.Groups[1];
                        var spriteName = match.Groups[2];
                        TpSheetFileId2Name[path][fileid.ToString()] = spriteName.ToString();
                        AssetInfo assetInfo2 = new AssetInfo(path + "@" + spriteName);
                        pathToAssetInfo.Add(assetInfo2.path, assetInfo2);
                    }                    
                }
                AssetInfo assetInfo = new AssetInfo(path);
                pathToAssetInfo.Add(assetInfo.path, assetInfo);    
            }

            // Find links between assets
            for (int p = 0; p < allAssetPaths.Length; p++) {
                if (p % 20 == 0) {
                    var cancel = EditorUtility.DisplayCancelableProgressBar("Building Dependency Database", allAssetPaths[p], (float)p / allAssetPaths.Length);
                    if (cancel) {
                        pathToAssetInfo = null;
                        break;
                    }
                }
                AddAssetToDatabase(allAssetPaths[p], recursive, calcTpSheet);
            }

            EditorUtility.ClearProgressBar();

            ProjectCuratorData.IsUpToDate = true;

            SaveDatabase();
        }

        public static void SaveDatabase()
        {
            if (pathToAssetInfo == null)
                return;
            var assetInfos = new AssetInfo[pathToAssetInfo.Count];
            int i = 0;
            foreach (var pair in pathToAssetInfo) {
                assetInfos[i] = pair.Value;
                i++;
            }
            ProjectCuratorData.AssetInfos = assetInfos;
            ProjectCuratorData.Save();
        }
    }
}