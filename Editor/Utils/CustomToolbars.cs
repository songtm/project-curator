using SFrame;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityToolbarExtender
{
    static class ToolbarStyles
    {
        public static readonly GUIStyle commandButtonStyle;
        public static readonly GUIContent RencentFile;
        public static readonly GUIContent RencentScene;
        public static readonly GUIContent HierarchyInfo;

        static ToolbarStyles()
        {
            commandButtonStyle = new GUIStyle("Command")
            {
                // fontSize = 16,
                // alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                // fontStyle = FontStyle.Bold
                fixedWidth = 30,
            };

            RencentFile = new GUIContent("",EditorGUIUtility.IconContent("Prefab Icon").image);
            RencentScene = new GUIContent("",EditorGUIUtility.IconContent("SceneAsset Icon").image);
            HierarchyInfo = new GUIContent("",EditorGUIUtility.IconContent("UnityEditor.SceneHierarchyWindow").image);
        }
    }

    [InitializeOnLoad]
    public class CustomToolbar
    {
        public static int ShowHierarchyInfo;//0, 1, 2详细
        static CustomToolbar()
        {
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
            ShowHierarchyInfo = PlayerPrefs.GetInt(nameof(ShowHierarchyInfo), 0);
        }

        static void OnToolbarGUI()
        {
            
            GUILayout.Space(8);
            
            GUI.changed = false;
            if (GUILayout.Button(ToolbarStyles.HierarchyInfo, ToolbarStyles.commandButtonStyle))
            {
                ShowHierarchyInfo = (ShowHierarchyInfo + 1) % 3;
                PlayerPrefs.SetInt(nameof(ShowHierarchyInfo), ShowHierarchyInfo);
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
            }
            
            if (GUILayout.Button(ToolbarStyles.RencentScene, ToolbarStyles.commandButtonStyle))
            {
                RecentFileList.ShowInBuildScenes();
            }
            GUILayout.Space(2);
            if (GUILayout.Button(ToolbarStyles.RencentFile, ToolbarStyles.commandButtonStyle))
            {
                RecentFileList.ShowMenu();   
            }
            
            // if(GUILayout.Button(new GUIContent("RecentFiles", "Player_low"), ToolbarStyles.commandButtonStyle))
            // {
            // 	SceneHelper.StartScene("Player_low");
            // }
            //
            // if(GUILayout.Button(new GUIContent("2", "SplashScene"), ToolbarStyles.commandButtonStyle))
            // {
            // 	SceneHelper.StartScene("SplashScene");
            // }
        }
    }
/*
    static class SceneHelper
    {
        static string sceneToOpen;

        public static void StartScene(string sceneName)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            sceneToOpen = sceneName;
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (sceneToOpen == null ||
                EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // need to get scene via search because the path to the scene
                // file contains the package version so it'll change over time
                string[] guids = AssetDatabase.FindAssets("t:scene " + sceneToOpen, null);
                if (guids.Length == 0)
                {
                    Debug.LogWarning("Couldn't find scene file");
                }
                else
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    EditorSceneManager.OpenScene(scenePath);
                    EditorApplication.isPlaying = true;
                }
            }

            sceneToOpen = null;
        }
    }
    */
}