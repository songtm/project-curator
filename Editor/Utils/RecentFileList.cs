// Author: songtianming
// DateTime: 2020年6月6日

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// ReSharper disable CheckNamespace

namespace SFrame
{
    public  class RecentFileList : ScriptableObject
    {
        private static string cachePath = "Packages/com.ogxd.project-curator/Editor/Utils//recentFilesCache.asset";
        
        [SerializeField] public List<string> recentFiles;
        
        private int maxCount = 20;

        private static RecentFileList _instance;
        public static RecentFileList Instance => _instance ? _instance : Load();
        private void OnEnable()
        {
            EditorApplication.hierarchyChanged -= HierarchyChanged;
            EditorApplication.hierarchyChanged += HierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= HierarchyChanged;
        }
        
        [InitializeOnLoadMethod]
        private static RecentFileList Load()
        {
            
            // if (_recenFilesObject != null) return;
            _instance = AssetDatabase.LoadAssetAtPath<RecentFileList>(cachePath);
            if (_instance == null)
            {
                _instance = ScriptableObject.CreateInstance<RecentFileList>();
                (new FileInfo(cachePath)).Directory?.Create();
                AssetDatabase.CreateAsset(_instance, cachePath);
                AssetDatabase.SaveAssets();
            }

            return _instance;
        }

        private void HierarchyChanged()
        {
            // Debug.Log("hir changed");
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null) return;
            var path = prefabStage.assetPath;
            if (string.IsNullOrEmpty(path)) return;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (guid == null) return;
            if (recentFiles.Contains(guid))
            {
                recentFiles.Remove(guid);
                recentFiles.Insert(0, guid);
            }
            else
            {
                recentFiles.Insert(0, guid);
            }

            if (recentFiles.Count > maxCount)
            {
                recentFiles.RemoveAt(recentFiles.Count - 1);
            }

            EditorUtility.SetDirty(this);
        }

        private static void AddMenuItem(GenericMenu menu, string menuPath, string assetPath)
        {
            var shortcut = $"(&{menuPath[0].ToString().ToUpper()})";
            menu.AddItem(new GUIContent(shortcut + menuPath), false, OnFileSelected, assetPath);
        }

        private static void OnFileSelected(object param)
        {
            var assetPath = param as string;
            if (assetPath.EndsWith(".prefab"))
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)));
            else if (assetPath.EndsWith(".unity"))
            {
                if (Application.isPlaying)
                {
                    CustomEditorUtil.ShowEditorTip("stop playing first", BuildInWinType.SceneHierarchyWindow);
                    return;
                }
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(assetPath);
            }
        }

        // [MenuItem("Window/-/Recent Files")]
        public static void ShowMenu()
        {
            if (Event.current == null) return;
            GenericMenu menu = new GenericMenu();
            foreach (var recentFile in Instance.recentFiles)
            {
                var path = AssetDatabase.GUIDToAssetPath(recentFile);
                if (!string.IsNullOrEmpty(path))
                {
                    var match = Regex.Match(path, @"/UI/(\w+)");
                    if (match.Success)
                    {
                        var moduleName = match.Groups[1];
                        AddMenuItem(menu, $"[{moduleName}] {Path.GetFileName(path)}", path);
                    }
                    else
                    {
                        AddMenuItem(menu, Path.GetFileName(path), path);
                    }
                }
            }

            // menu.ShowAsContext();
            menu.DropDown(new Rect(Event.current.mousePosition.x + 5, Event.current.mousePosition.y + 16, 0f, 0f));
        }


        public static void ShowInBuildScenes()
        {
            if (Event.current == null) return;
            GenericMenu menu = new GenericMenu();

            int sceneCount = SceneManager.sceneCountInBuildSettings;
            var added = new HashSet<string>();
            for (int i = 0; i < sceneCount; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                AddMenuItem(menu, Path.GetFileName(scenePath), scenePath);
                added.Add(scenePath);
            }
            menu.AddSeparator("");
            
            var scenesGUIDs = AssetDatabase.FindAssets("t:Scene");
            var scenesPaths = scenesGUIDs.Select(AssetDatabase.GUIDToAssetPath);
            foreach (var scenePath in scenesPaths)
            {
                if (!added.Contains(scenePath))
                {
                    AddMenuItem(menu, Path.GetFileName(scenePath), scenePath);    
                }
            }

            // menu.ShowAsContext();
            menu.DropDown(new Rect(Event.current.mousePosition.x + 5, Event.current.mousePosition.y + 16, 0f, 0f));
        }
    }
}