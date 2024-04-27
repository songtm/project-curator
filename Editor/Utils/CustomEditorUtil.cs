// Author: songtianming
// DateTime: 2020.05.25

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SFrame
{
    public enum BuildInWinType
    {
        ProjectBrowser,
        SceneView,
        ConsoleWindow,
        Inspector,
        PlayModeView,
        SceneHierarchyWindow,
    }

    public enum FilterMode
    {
        All,
        Name,
        Type,
    }

    public static class CustomEditorUtil
    {
        private static readonly GUIStyle PlaceholderTextStyle;

        private static Dictionary<BuildInWinType, string> winNameMap = new Dictionary<BuildInWinType, string>
        {
            {BuildInWinType.ProjectBrowser, "UnityEditor.ProjectBrowser"},
            {BuildInWinType.SceneView, "UnityEditor.SceneView"},
            {BuildInWinType.ConsoleWindow, "UnityEditor.ConsoleWindow"},
            {BuildInWinType.Inspector, "UnityEditor.InspectorWindow"},
            {BuildInWinType.PlayModeView, "UnityEditor.PlayModeView"},
            {BuildInWinType.SceneHierarchyWindow, "UnityEditor.SceneHierarchyWindow"},
        };

        static CustomEditorUtil()
        {
            PlaceholderTextStyle = new GUIStyle(EditorStyles.textField)
            {
                fontStyle = FontStyle.Italic,
            };
            PlaceholderTextStyle.normal.textColor = Color.grey;
        }

        public static void SetHirarchySearchFilter(string filter, FilterMode filterMode = FilterMode.All)
        {
            var hierarchy = Resources
                .FindObjectsOfTypeAll<SearchableEditorWindow>()
                .FirstOrDefault(c => c.GetType().ToString() == "UnityEditor.SceneHierarchyWindow");

            if (hierarchy == null) return;

            var setSearchType = typeof(SearchableEditorWindow)
                    .GetMethod("SetSearchFilter", BindingFlags.NonPublic | BindingFlags.Instance)
                ;

            var parameters = new object[] {filter, (int) filterMode, false, false};

            setSearchType.Invoke(hierarchy, parameters);
        }

        //"UnityEditor.ProjectBrowser"  "UnityEditor.SceneView" "UnityEditor.ConsoleWindow"
        public static EditorWindow VisibleBuiltinWindow(BuildInWinType winType)
        {
            var typeinfo = typeof(Editor).Assembly.GetType(winNameMap[winType]);
            try
            {
                var win = EditorWindow.GetWindow(typeinfo);
                win.Show();
                return win;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void VisibleInProjectWindow(Object obj, bool ping = true)
        {
            Selection.activeObject = obj;
            EditorUtility.FocusProjectWindow();
            if (ping) EditorGUIUtility.PingObject(obj);
        }
        public static void VisibleInInspector(Component com, string keyword, bool pinObj)
        {
            Selection.activeGameObject = com.gameObject;
            Highlighter.Stop();
            if (pinObj) EditorGUIUtility.PingObject(com.gameObject);
            
            VisibleBuiltinWindow(BuildInWinType.Inspector);
            if (string.IsNullOrEmpty(keyword)) keyword = com.GetType().Name;
            EditorApplication.delayCall += () =>
            {
                var count = 0;
                void Func()
                {
                    ExpandAll();
                    count++;
                    EditorApplication.delayCall += () =>
                    {
                        var res = Highlighter.Highlight("Inspector", keyword, HighlightSearchMode.Auto);
                        if (res == false && count < 3)
                        {
                            Func();
                        }
                    };  
                }
                Func();
            };
        }
        
        [MenuItem("CONTEXT/Component/Collapse All")]
        private static void CollapseAll()
        {
            SetAllInspectorsExpanded(false);
        }
 
        [MenuItem("CONTEXT/Component/Expand All")]
        private static void ExpandAll()
        {
            SetAllInspectorsExpanded(true);
        }
 
        private static void SetAllInspectorsExpanded(bool expanded)
        {
            var activeEditorTracker = ActiveEditorTracker.sharedTracker;
 
            for (var i = 0; i < activeEditorTracker.activeEditors.Length; i++)
            {
                activeEditorTracker.SetVisible(i, expanded ? 1 : 0);
            }
        }
        
        public static void ShowEditorTip(string msg, BuildInWinType winType, float duration = 0.5f)
        {
            var typeinfo = typeof(UnityEditor.Editor).Assembly.GetType(winNameMap[winType]);
            var win = EditorWindow.GetWindow(typeinfo);
            win.ShowNotification(new GUIContent(msg), duration);
        }

        public static string TextInput(string text, string placeholder, bool area = false,
            params GUILayoutOption[] options)
        {
            var newText = area ? EditorGUILayout.TextArea(text, options) : EditorGUILayout.TextField(text, options);
            if (String.IsNullOrEmpty(text.Trim()))
            {
                const int textMargin = 2;
                var textRect = GUILayoutUtility.GetLastRect();
                var position = new Rect(textRect.x + textMargin, textRect.y, textRect.width, textRect.height);
                EditorGUI.LabelField(position, placeholder, PlaceholderTextStyle);
            }

            return newText;
        }
        
        public static void  OpenScriptFile(MonoBehaviour behaviour, params string[] searchStrings)
        {
            bool DoSearch(string[] lines, out int col, out int lineNumber, string searchStr)
            {
                lineNumber = 1;
                col = 1;
                if (string.IsNullOrEmpty(searchStr)) return true;
                for (var i = 0; i < lines.Length; i++)
                {
                    col = lines[i].IndexOf(searchStr);

                    if (col < 0)
                    {
                        var math = Regex.Match(lines[i], searchStr);
                        if (math.Success)
                        {
                            col = math.Index;
                            if (math.Groups.Count > 1)
                            {
                                col += math.Groups[1].Value.Length;
                            }
                        }
                    }

                    if (col >= 0)
                    {
                        lineNumber = i + 1;
                        col++;
                        return true;
                    }
                }

                return false;
            }

            var script = MonoScript.FromMonoBehaviour(behaviour);

            if (searchStrings.Length == 0)
            { 
                AssetDatabase.OpenAsset(script);
            }
            else
            {
                var path = AssetDatabase.GetAssetPath(script);
                var lines = File.ReadAllLines(path);
                var col = 1;
                var lineNumber = 1;
                foreach (var searchString in searchStrings)
                {
                    if (DoSearch(lines, out col, out lineNumber, searchString)) break;
                }
                
                AssetDatabase.OpenAsset(script, lineNumber, col);
            }
        }
    }
}