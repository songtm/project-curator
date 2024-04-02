// Author: songtianming
// DateTime: 2020年8月23日

using Ogxd.ProjectCurator;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;

namespace SFrame
{
    [InitializeOnLoad]
    static class CustomProjectView
    {
        private static GUIStyle _labelStyle;

        private static GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle != null) return _labelStyle;
                _labelStyle = new GUIStyle(EditorStyles.label);
                var clr = _labelStyle.normal.textColor;
                clr.a = 0.6f;
                _labelStyle.normal.textColor = clr; 
                return _labelStyle;
            }
        }

        static CustomProjectView()
        {
            // ReSharper disable once DelegateSubtraction
            EditorApplication.projectWindowItemOnGUI -= DrawAssetDetails;
            EditorApplication.projectWindowItemOnGUI += DrawAssetDetails;
            // EditorApplication.projectWindowItemInstanceOnGUI//unity 2022才有可以自定义sub asset 显示
        }


        private static void DrawAssetDetails(string guid, Rect rect)
        {
            if (CustomToolbar.ShowHierarchyInfo == 0) return;
            if (Event.current.type != EventType.Repaint || !IsMainListAsset(rect)) return;

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) return; // this entry could be Favourites or Packages. Ignore it.

            Rect LabelRect(string str)
            {
                float width = GUI.skin.label.CalcSize(new GUIContent(str)).x + 16;
                var res = rect;
                res.x += res.width - width;
                res.width = width;
                return res;
            }

            var showDetail = CustomToolbar.ShowHierarchyInfo;
            var label = "";
            if (asset is Texture2D tex)
            {
                // Right align label:
                label = $"{tex.width}*{tex.height}";
                if (showDetail >= 1)
                {
                    var assetInfo = ProjectCurator.GetAsset(assetPath);//依赖了ProjectCurator的build
                    if (assetInfo != null)
                    {
                        if (!assetInfo.IsRefBySpriteAtlas())
                        {
                            
                            if (tex.format.ToString().StartsWith("RGB"))
                            {
                                label = tex.format + " " + label;    
                            }
                        }
                        else
                        {
                            // label = "Atlas " + label;
                        }
                    }
                    else
                    {
                        label = "*Curator " + label;
                    }
                    
                }
                // EditorGUI.DrawRect(rect, new Color(1, 0, 0, 0.4f));
            }
            else if (asset is Mesh mesh)
            {
                label = $"{mesh.vertexCount}";
            }
            else if (asset is Material mat)
            {
                label = $"Q{mat.renderQueue}";
            }
            else if (asset is GameObject go)
            {
                // var coms = go.GetComponents<MonoBehaviour>();
                // foreach (var com in coms)
                // {
                //     var type = com != null ? com.GetType() : typeof(Missing);
                //     label += type.Name;
                // }
            }

            if (!string.IsNullOrEmpty(label))
            {
                // if (warning)
                //     GUI.Label(LabelRect(label), label, LabelStyleRed);
                // else
                GUI.Label(LabelRect(label), label, LabelStyle);
            }
        }

        private static bool IsMainListAsset(Rect rect)
        {
            // Don't draw details if project view shows large preview icons:
            if (rect.height > 20)
            {
                return false;
            }

            // Don't draw details if this asset is a sub asset:
            if (rect.x > 16)
            {
                return false;
            }

            return true;
        }
    }
}