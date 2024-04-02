using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ogxd.ProjectCurator
{
    public enum IncludedInBuild
    {
        Unknown = 0,
        // Not included in build
        NotIncludable = 1,
        NotIncluded = 2,
        // Included in build
        SceneInBuild = 10,
        RuntimeScript = 11,
        ResourceAsset = 12,
        Referenced = 13,
    }

    [Serializable]
    public class AssetInfo : ISerializationCallbackReceiver
    {

        [NonSerialized]
        public HashSet<string> referencers = new HashSet<string>();

        [NonSerialized]
        public HashSet<string> dependencies = new HashSet<string>();

        [SerializeField]
        public string[] _references;

        [SerializeField]
        public string[] _dependencies;

        public void OnBeforeSerialize()
        {
            _references = referencers.ToArray();
            _dependencies = dependencies.ToArray();
        }

        public void OnAfterDeserialize()
        {
            referencers = new HashSet<string>(_references ?? new string[0]);
            dependencies = new HashSet<string>(_dependencies ?? new string[0]);
        }

        [SerializeField]
        public string path;

        public AssetInfo(string path)
        {
            this.path = path;
        }

        private string[] GetDependInternal(string path, bool recursive = true, bool calcTpSheet = false)
        {
            if (path.EndsWith(".shadergraph"))//shadergraph打包后为shader,里面强制处理成不引用其它资源
            {
                return new string[0];
            }
            var deps = AssetDatabase.GetDependencies(path, recursive);
            var newDeps = new HashSet<string>();
            foreach (var dep in deps)
            {
                newDeps.Add(dep);
                if (calcTpSheet && dep.EndsWith(".png") && dep.StartsWith(ProjectCurator.TpSheetPath))
                {
                    var tpsheetguid = AssetDatabase.AssetPathToGUID(dep);
                    var allText = File.ReadAllText(path);
                    var matches = Regex.Matches(allText, @"{fileID: (.*), guid: " + tpsheetguid);
                    foreach (Match match in matches)
                    {
                        var fileid = match.Groups[1];
                        if (ProjectCurator.TpSheetFileId2Name[dep].TryGetValue(fileid.ToString(), out var spname))
                        {
                            newDeps.Add(dep + "@" + spname);    
                        }
                    }
                }
            }
            return newDeps.ToArray();
        }
        public string[] GetDependencies(bool recursive = true, bool calcTpSheet = false)
        {
            //songtm 2020.09.15 索引lua支持!
            if (path.EndsWith("config_ui_info.lua"))
            {
                var readLines = File.ReadLines(path);
                List<string> res = new List<string>();
                foreach (var line in readLines)
                {
                    var indexOf = line.IndexOf("guid = \"", StringComparison.Ordinal);
                    if (indexOf > 0 && line.Length > indexOf + 40)
                    {
                        var prefabguid = line.Substring(indexOf + 8, 32);
                        var assetPath = AssetDatabase.GUIDToAssetPath(prefabguid);
                        if (!string.IsNullOrEmpty(assetPath))
                            res.Add(assetPath);
                    }
                }

                return res.ToArray();
            }
            if (path.EndsWith(".prefab") && path.StartsWith("Assets/XRes/UI/"))
            {
                var allText = File.ReadAllText(path);
                var index = allText.IndexOf("m_AssetGUID: ", StringComparison.Ordinal);//支持prefab中显示对lua的引用?
                if (index >= 0)
                {
                    var luaguid = allText.Substring(index + 13, 32);
                    var assetPath = AssetDatabase.GUIDToAssetPath(luaguid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var deps = GetDependInternal(path, recursive);
                        var res = new string[deps.Length + 1];
                        deps.CopyTo(res, 0);
                        res[res.Length - 1] = assetPath;
                        return res;
                    }
                }
            }
            return GetDependInternal(path, recursive, calcTpSheet);
        }

        public void ClearIncludedStatus()
        {
            includedStatus = IncludedInBuild.Unknown;
        }

        public bool IsRefBySpriteAtlas()
        {
            foreach (var referencer in referencers)
            {
                if (referencer.EndsWith(".spriteatlas")) return true;
            }

            return false;
        }
        [NonSerialized]
        private IncludedInBuild includedStatus;

        public IncludedInBuild IncludedStatus {
            get {
                if (includedStatus != IncludedInBuild.Unknown)
                    return includedStatus;
                // Avoid circular loops
                includedStatus = IncludedInBuild.NotIncluded;
                return includedStatus = CheckIncludedStatus();
            }
        }

        public bool IsIncludedInBuild => (int)IncludedStatus >= 10;

        private IncludedInBuild CheckIncludedStatus()
        {

            foreach (var referencer in referencers) {
                AssetInfo refInfo = ProjectCurator.GetAsset(referencer);
                if (refInfo.IsIncludedInBuild) {
                    return IncludedInBuild.Referenced;
                }
            }

            bool isInEditor = false;

            string[] directories = path.ToLower().Split('/');
            for (int i = 0; i < directories.Length - 1; i++) {
                switch (directories[i]) {
                    case "editor":
                        isInEditor = true;
                        break;
                    case "resources":
                        return IncludedInBuild.ResourceAsset;
                    case "plugins":
                        break;
                    default:
                        break;
                }
            }

            string extension = System.IO.Path.GetExtension(path);
            switch (extension) {
                case ".cs":
                    if (isInEditor) {
                        return IncludedInBuild.NotIncludable;
                    } else {
                        return IncludedInBuild.RuntimeScript;
                    }
                case ".unity":
                    if (EditorBuildSettings.scenes.Select(x => x.path).Contains(path))
                        return IncludedInBuild.SceneInBuild;
                    break;
                // Todo : Handle DLL
                // https://docs.unity3d.com/ScriptReference/Compilation.Assembly-compiledAssemblyReferences.html
                // CompilationPipeline
                // Assembly Definition
                default:
                    break;
            }

            return IncludedInBuild.NotIncluded;
        }
    }
}