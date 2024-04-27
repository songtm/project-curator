using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SFrame;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
// using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace Ogxd.ProjectCurator
{
    public class ProjectCuratorWindow : EditorWindow, IHasCustomMenu
    {

        [MenuItem("Tools/Project Curator[✂Asset Reference]", false, 50)]
        static void Init()
        {
            GetWindow<ProjectCuratorWindow>("Project Curator");
        }

        public ProjectCuratorWindow()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            Repaint();
        }

        private Vector2 scroll;

        private bool locked = false;
        private bool fullPath = true;
        private bool showCSfile = true;
        private bool includePackage = false;
        private string lockedguid = null;

        private bool dependenciesOpen = true;
        private bool referencesOpen = true;
        private Dictionary<string, bool> depBundleFoldout = new Dictionary<string, bool>();
        private Dictionary<string, bool> refBundleFoldout = new Dictionary<string, bool>();

        private static GUIStyle titleStyle;
        private static GUIStyle TitleStyle => titleStyle ?? (titleStyle = new GUIStyle(EditorStyles.label) { fontSize = 13 });

        private static GUIStyle itemStyle;
        private static GUIStyle ItemStyle => itemStyle ?? (itemStyle = new GUIStyle(EditorStyles.label) { margin = new RectOffset(48, 0, 0, 0) });

        private void OnGUI()
        {
            string selectedPath = AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
            if (selectedPath.StartsWith(ProjectCurator.TpSheetPath) && Selection.activeObject is Sprite)
            {
                selectedPath += "@" + Selection.activeObject.name;
            }
            if (lockedguid != null)////songtm
            {
                var lockedPath = AssetDatabase.GUIDToAssetPath(lockedguid);
                if (!string.IsNullOrEmpty(lockedPath)) selectedPath = lockedPath;
            }
            if (string.IsNullOrEmpty(selectedPath))
                return;

            // Standard spacing to mimic Unity's Inspector header
            GUILayout.Space(2);

            Rect rect;

            GUILayout.BeginHorizontal("In BigTitle");
            // GUILayout.Label(AssetDatabase.GetCachedIcon(selectedPath), GUILayout.Width(36), GUILayout.Height(36));
            if (GUILayout.Button(AssetDatabase.GetCachedIcon(selectedPath), GUILayout.Width(36), GUILayout.Height(36)))
            {
                CustomEditorUtil.SetHirarchySearchFilter("ref:" + selectedPath);
            }
            GUILayout.BeginVertical();
            // GUILayout.Label(Path.GetFileName(selectedPath), TitleStyle);
            if (GUILayout.Button(new GUIContent(Path.GetFileName(selectedPath), "reveal in project window")))//songtm
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(selectedPath);
                CustomEditorUtil.VisibleInProjectWindow(obj);
            }
            // Display directory (without "Assets/" prefix)
            GUILayout.Label(Regex.Match(Path.GetDirectoryName(selectedPath), "(\\\\.*)$").Value);
            rect = GUILayoutUtility.GetLastRect();
            GUILayout.EndVertical();
            GUILayout.Space(44);
            //////////songtm
            GUILayout.BeginVertical();
            locked = GUILayout.Toggle(locked, "Lock");
            if (locked)
            {
                if (lockedguid == null)
                {
                    lockedguid = AssetDatabase.AssetPathToGUID(selectedPath);
                }
            }
            else
            {
                lockedguid = null;
            }
            if (GUILayout.Button("New", GUILayout.MaxWidth(50)))
            {
                CreateWindow<ProjectCuratorWindow>("Project Curator");
            }
            
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            fullPath = GUILayout.Toggle(fullPath, "fullPath");
            showCSfile = GUILayout.Toggle(showCSfile, "showC#");
            GUILayout.EndVertical();
            
            includePackage = GUILayout.Toggle(includePackage, "IndexPkg");
            //////////end
            GUILayout.EndHorizontal();

            if (Directory.Exists(selectedPath))
                return;

            AssetInfo selectedAssetInfo = ProjectCurator.GetAsset(selectedPath);
            if (selectedAssetInfo == null) {
                bool rebuildClicked = HelpBoxWithButton(new GUIContent("You must rebuild database to obtain information on this asset", EditorGUIUtility.IconContent("console.warnicon").image), new GUIContent("Rebuild Database"));
                if (rebuildClicked) {
                    ProjectCurator.RebuildDatabase();
                }
                return;
            }

            var content = new GUIContent(selectedAssetInfo.IsIncludedInBuild ? ProjectIcons.LinkBlue : ProjectIcons.LinkBlack, selectedAssetInfo.IncludedStatus.ToString());
            GUI.Label(new Rect(position.width - 20, rect.y + 1, 16, 16), content);

            scroll = GUILayout.BeginScrollView(scroll);

            dependenciesOpen = EditorGUILayout.Foldout(dependenciesOpen, $"Dependencies ({selectedAssetInfo.dependencies.Count})");
            if (dependenciesOpen)
            {
                EditorGUI.indentLevel = 1;
                var dic = GroupAssets(selectedAssetInfo.dependencies);
                foreach (var pair in dic)
                {
                    var bundle = pair.Key;
                    if (!depBundleFoldout.TryGetValue(bundle, out var folded))
                    {
                        folded = true;
                    }
                    
                    depBundleFoldout[bundle] = EditorGUILayout.Foldout(folded, $"{bundle} ({pair.Value.Count})");
                    if (!depBundleFoldout[bundle]) continue;
                    string preDir = null;
                    foreach (var dependency in pair.Value)
                    {
                        var realPath = dependency;
                        // var indexOf = dependency.IndexOf('@');
                        // if (indexOf >= 0) realPath = dependency.Substring(0, indexOf);

                        if (GUILayout.Button(fullPath ? dependency : Path.GetFileName(dependency), ItemStyle))
                        {
                            var activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(realPath);
                            CustomEditorUtil.VisibleInProjectWindow(activeObject);
                        }
                        rect = GUILayoutUtility.GetLastRect();
                        var curDir = Path.GetDirectoryName(dependency);
                        if (fullPath &&(preDir != null && curDir != preDir))
                        {
                            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1), Color.blue);
                        }
                        preDir = curDir;
                        
                        var cachedIcon = AssetDatabase.GetCachedIcon(realPath);
                        if (cachedIcon != null)//songtm资源删除后 cacheIcon为null,但database并未更新时
                        {
                            GUI.DrawTexture(new Rect(rect.x -16, rect.y, rect.height, rect.height), cachedIcon);
                        }
                        AssetInfo depInfo = ProjectCurator.GetAsset(dependency);
                        content = new GUIContent(depInfo.IsIncludedInBuild ? ProjectIcons.LinkBlue : ProjectIcons.LinkBlack, depInfo.IncludedStatus.ToString());
                        GUI.Label(new Rect(rect.width + rect.x - 20, rect.y + 1, 16, 16), content);
                    }
                }
            }

            GUILayout.Space(6);
            EditorGUI.indentLevel = 0;
            referencesOpen = EditorGUILayout.Foldout(referencesOpen, $"Referencers ({selectedAssetInfo.referencers.Count})");
            if (referencesOpen) {
                EditorGUI.indentLevel = 1;
                var dic = GroupAssets(selectedAssetInfo.referencers);
                foreach (var pair in dic)
                {
                    var bundle = pair.Key;
                    if (!refBundleFoldout.TryGetValue(bundle, out var folded))
                    {
                        folded = true;
                    }
                    refBundleFoldout[bundle] = EditorGUILayout.Foldout(folded, $"{bundle} ({pair.Value.Count})");
                    if (!refBundleFoldout[bundle]) continue;

                    foreach (var referencer in pair.Value)
                    {
                        if (GUILayout.Button(fullPath ? referencer : Path.GetFileName(referencer), ItemStyle)) {
                            var activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(referencer);
                            CustomEditorUtil.VisibleInProjectWindow(activeObject);
                        }
                        rect = GUILayoutUtility.GetLastRect();
                        var cachedIcon = AssetDatabase.GetCachedIcon(referencer);
                        if (cachedIcon != null) //songtm资源删除后 cacheIcon为null,但database并未更新时
                        {
                            GUI.DrawTexture(new Rect(rect.x - 16, rect.y, rect.height, rect.height), cachedIcon);
                        }

                        AssetInfo refInfo = ProjectCurator.GetAsset(referencer);
                        content = new GUIContent(refInfo.IsIncludedInBuild ? ProjectIcons.LinkBlue : ProjectIcons.LinkBlack, refInfo.IncludedStatus.ToString());
                        GUI.Label(new Rect(rect.width + rect.x - 20, rect.y + 1, 16, 16), content);
                    }
                }
            }
            EditorGUI.indentLevel = 0;
            GUILayout.Space(5);

            GUILayout.EndScrollView();

            if (!selectedAssetInfo.IsIncludedInBuild) {
                bool deleteClicked = HelpBoxWithButton(new GUIContent("This asset is not referenced and never used. Would you like to delete it ?", EditorGUIUtility.IconContent("console.warnicon").image), new GUIContent("Delete Asset"));
                if (deleteClicked)
                {
                    var delPath = selectedPath;
                    if (selectedPath.Contains("@"))
                    {
                        var indexOf = selectedPath.IndexOf('@');
                        var realPath = selectedPath.Substring(0, indexOf);
                        var subname = selectedPath.Substring(indexOf + 1);
                        delPath = ProjectCurator.TpSrcPath + Path.GetFileNameWithoutExtension(realPath) + "/" +
                                      subname + ".png";
                        delPath = Path.GetFullPath(delPath);
                    }
                    if (EditorUtility.DisplayDialog("delete file?", delPath, "ok", "cancel"))
                    {
                        File.Delete(delPath);
                        AssetDatabase.Refresh();
                        ProjectCurator.RemoveAssetFromDatabase(selectedPath);    
                    }
                }
            }
        }

        Dictionary<string, List<string>> GroupAssets(HashSet<string> set)
        {
            var dic = new Dictionary<string, List<string>>();
            foreach (var s in set)
            {
                if (!showCSfile && s.EndsWith(".cs")) continue;
                var bundle = GetBundleNameStr(null, s);
                if (!dic.ContainsKey(bundle))
                {
                    dic[bundle] = new List<string>();
                }
                dic[bundle].Add(s);
            }
            foreach (var keyValuePair in dic)
            {
                keyValuePair.Value.Sort();
            }
            return dic;
        }
        
        string GetBundleNameStr(string self, string assetpath)
        {/*
            if (BuildScriptPackedMode.AssetName2BundleName.TryGetValue(assetpath, out var bundleName))
            {
                // bundleName = "[" + bundleName + "] ";
            }

            return bundleName ?? "not in bundle";*/
            return Path.GetExtension(assetpath);
        }
        
        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Rebuild Database 直接间接引用"), false, () => ProjectCurator.RebuildDatabase(true, false, includePackage));
            menu.AddItem(new GUIContent("Rebuild Database 直接引用"), false, () => ProjectCurator.RebuildDatabase(false, false, includePackage));
            menu.AddItem(new GUIContent("Rebuild Database 直接引用+TP图集子图引用"), false, () => ProjectCurator.RebuildDatabase(false, true, includePackage));
            menu.AddItem(new GUIContent("Build Deps graph"), false, ProjectCurator.BuildDepsGraph);
            menu.AddItem(new GUIContent("Clear Database"), false, ProjectCurator.ClearDatabase);
            // menu.AddItem(new GUIContent("Project Overlay"), ProjectWindowOverlay.Enabled, () => { ProjectWindowOverlay.Enabled = !ProjectWindowOverlay.Enabled; });
        }


        public bool HelpBoxWithButton(GUIContent messageContent, GUIContent buttonContent)
        {
            float buttonWidth = buttonContent.text.Length * 8;
            const float buttonSpacing = 5f;
            const float buttonHeight = 18f;

            // Reserve size of wrapped text
            Rect contentRect = GUILayoutUtility.GetRect(messageContent, EditorStyles.helpBox);
            // Reserve size of button
            GUILayoutUtility.GetRect(1, buttonHeight + buttonSpacing);

            // Render background box with text at full height
            contentRect.height += buttonHeight + buttonSpacing;
            GUI.Label(contentRect, messageContent, EditorStyles.helpBox);

            // Button (align lower right)
            Rect buttonRect = new Rect(contentRect.xMax - buttonWidth - 4f, contentRect.yMax - buttonHeight - 4f, buttonWidth, buttonHeight);
            return GUI.Button(buttonRect, buttonContent);
        }
    }
}