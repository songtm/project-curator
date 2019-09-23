﻿using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ogxd.DependencyHighlighter
{

    public class DependencyInfoPopup : PopupWindowContent
    {

        private DependencyInfoHighlighter.DependencyInfo dependencyInfo;

        public DependencyInfoPopup(DependencyInfoHighlighter.DependencyInfo dependencyInfo)
        {
            this.dependencyInfo = dependencyInfo;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(600, 20 * (dependencyInfo.referencers.Count + dependencyInfo.dependencies.Count + 5));
        }

        public override void OnGUI(Rect rect)
        {

            if (dependencyInfo.Included == IncludedInBuild.NotIncluded)
                GUILayout.Label($"Not In Build", EditorStyles.boldLabel);
            else
                GUILayout.Label($"In Build ({dependencyInfo.Included})", EditorStyles.boldLabel);

            GUILayout.Label($"Referencers ({dependencyInfo.referencers.Count})", EditorStyles.boldLabel);
            if (dependencyInfo.referencers.Count > 0)
            {
                //GUILayout.Label(string.Join("\n", dependencyInfo.referencers.Select(x => x.path)));
                foreach (var referencer in dependencyInfo.referencers)
                {
                    if (GUILayout.Button(referencer.path, EditorStyles.label))
                    {
                        Selection.activeObject = referencer.Asset;
                    }
                }
            }

            GUILayout.Label($"Dependencies ({dependencyInfo.dependencies.Count})", EditorStyles.boldLabel);
            if (dependencyInfo.dependencies.Count > 0)
            {
                //GUILayout.Label(string.Join("\n", dependencyInfo.dependencies.Select(x => x.path)));
                foreach (var dependency in dependencyInfo.dependencies)
                {
                    if (GUILayout.Button(dependency.path, EditorStyles.label))
                    {
                        Selection.activeObject = dependency.Asset;
                    }
                }
            } 
        }
    }
}