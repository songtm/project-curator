// Author: songtianming
// DateTime: Dec 24, 2019 18:20

using System;
using System.Collections.Generic;
using System.Linq;
using AutoBind;
using UnityEditor;
#if UNITY_2021_1_OR_NEWER
    using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityToolbarExtender;
using Object = UnityEngine.Object;

namespace SFrame
{
    class Missing
    {
    }

    [InitializeOnLoad]
    static class CustomHierarchy
    {
        private static GameObject _root;
        private static Color _maskClr = new Color(1, 0.0f, 0f, 0.6f);
        private static bool _treeViewDirty = true;
        private static readonly Dictionary<int, int> InjectPaths = new Dictionary<int, int>();
        private static GUIStyle _labelStyle;
        private static GUIStyle _labelStyleDisabled;
        private static GUIStyle _labelStyleBuildIn;
        private static GUIStyle _labelStyleBuildInDisabled;

        private static GameObject _lastSelGo;

        private static Color _blueOrYellow;

        static CustomHierarchy()
        {
            _blueOrYellow = EditorGUIUtility.isProSkin ? Color.yellow : Color.blue;
            _labelStyle = new GUIStyle();
            _labelStyle.normal.textColor = _blueOrYellow;
            _labelStyle.clipping = TextClipping.Clip;

            _labelStyleDisabled = new GUIStyle(_labelStyle);
            _labelStyleDisabled.normal.textColor = new Color(_blueOrYellow.r, _blueOrYellow.g, _blueOrYellow.b, 0.3f);

            _labelStyleBuildIn = new GUIStyle(_labelStyle);
            _labelStyleBuildIn.fontStyle = FontStyle.Italic;
            _labelStyleBuildIn.normal.textColor = new Color(_blueOrYellow.r, _blueOrYellow.g, _blueOrYellow.b, 0.8f);
            _labelStyleBuildIn.fontSize = 11;

            _labelStyleBuildInDisabled = new GUIStyle(_labelStyleBuildIn);
            _labelStyleBuildInDisabled.normal.textColor =
                new Color(_blueOrYellow.r, _blueOrYellow.g, _blueOrYellow.b, 0.3f);

            EditorApplication.hierarchyWindowItemOnGUI -= OnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
            Selection.selectionChanged -= SelectionChanged;
            Selection.selectionChanged += SelectionChanged;
            EditorApplication.hierarchyChanged -= EditorApplicationOnHierarchyChanged;
            EditorApplication.hierarchyChanged += EditorApplicationOnHierarchyChanged;
            AutoBinder.InejctChanged -= OnInjectChanged;
            AutoBinder.InejctChanged += OnInjectChanged;
        }

        private static void OnInjectChanged()
        {
            _treeViewDirty = true;
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void EditorApplicationOnHierarchyChanged()
        {
            _treeViewDirty = true;
        }

        private static void SelectionChanged()
        {
            if (Selection.activeGameObject)
                _lastSelGo = Selection.activeGameObject;
        }


        private static readonly List<Type> ComTypes = new List<Type>
        {
            typeof(Canvas), typeof(Button), typeof(Toggle),
            typeof(Slider), typeof(Text), typeof(Scrollbar), typeof(ScrollRect),
            typeof(LayoutGroup), typeof(RectMask2D), typeof(Mask),
            typeof(Image), typeof(RawImage), typeof(MeshRenderer), typeof(SkinnedMeshRenderer), typeof(Collider),
            typeof(ParticleSystem), typeof(NavMeshAgent), typeof(Animator)
        };

        private static void DrawComponents(GameObject go, Rect selectionRect, float gonameWith)
        {

            var allComs = go.GetComponents<Component>();
            var startX = selectionRect.xMin + gonameWith;
            foreach (var ct in ComTypes)
            {
                var c = go.GetComponent(ct);
                if (c != null)
                {
                    GUIContent content = EditorGUIUtility.ObjectContent(c, null);
                    startX += 16;
                    GUI.DrawTexture(new Rect(startX, selectionRect.yMin, 16, 16), content.image);
                }
            }

            var curX = startX + 16 + 4;
            var showAll = CustomToolbar.ShowHierarchyInfo != 1;
            foreach (var behaviour in allComs)
            {
                var type = behaviour != null ? behaviour.GetType() : typeof(Missing);

                var customNamespace = type.Namespace == null || !type.Namespace.StartsWith("UnityEngine");
                if (showAll || (!showAll && customNamespace))
                {
                    if (behaviour is Transform || behaviour is CanvasRenderer) continue;
                    var width = DrawComItem(selectionRect, type, curX, behaviour, customNamespace);
                    curX += width;
                }
            }

            // foreach (var behaviour in go.GetComponents<Component>())
            // {
            //     var type = behaviour != null ? behaviour.GetType() : typeof(Missing);
            //     if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
            //     {
            //         var width = DrawComItem(selectionRect, type, curX, behaviour);
            //
            //         curX += width;
            //     }
            // }
        }

        private static float DrawComItem(Rect selectionRect, Type type, float curX, Component com, bool customNamespace)
        {
            var name = type.Name;
            if (typeof(IAutoBindable).IsAssignableFrom(type) || ReferenceView.InjectedComs.Contains(com))
            {
                name = "#" + name;
            }

            float width = GUI.skin.label.CalcSize(new GUIContent(name)).x + 2;
            // width = Mathf.Min(width, selectionRect.xMax - startX - 4);


            if (curX >= selectionRect.xMax) return width;

            if (com is MonoBehaviour behaviour)
            {
                if (
                    GUI.Button(new Rect(curX, selectionRect.yMin, width, 16), name,
                        behaviour.enabled ? _labelStyle : _labelStyleDisabled))
                {
                    Highlighter.Stop();
                    // Debug.Log($"click:{com.gameObject.name} {com.GetType().Name} {Event.current.button}");
                    var script = MonoScript.FromMonoBehaviour(behaviour);
                    var path = AssetDatabase.GetAssetPath(script);
                    if (Event.current.button == 1) //right
                    {
                        AssetDatabase.OpenAsset(script);
                    }
                    else if (Event.current.button == 2) //mid
                    {
                        CustomEditorUtil.VisibleBuiltinWindow(BuildInWinType.Inspector);
                        CustomEditorUtil.VisibleInInspector(com, null, true);
                    }
                    else if (Event.current.button == 0) //left
                    {
                        CustomEditorUtil.VisibleInProjectWindow(AssetDatabase.LoadAssetAtPath<Object>(path));
                    }
                }
            }
            else
            {
                var enabled = true;
                if (com is Renderer renderer) enabled = renderer.enabled;
                GUI.Label(new Rect(curX, selectionRect.yMin, width, 16), name,
                    enabled ? _labelStyleBuildIn : _labelStyleBuildInDisabled);
            }

            return width;
        }


        private static void OnGUI(int instanceId, Rect selectionRect)
        {
            if (CustomToolbar.ShowHierarchyInfo == 0) return;

            GameObject go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null) return;
            if (go == _lastSelGo) //选中行高亮
            {
                EditorGUI.DrawRect(
                    new Rect(selectionRect.x - 200, selectionRect.yMax - 2, selectionRect.width + 500, 2),
                    _blueOrYellow);
            }

            // if (PrefabStageUtility.GetCurrentPrefabStage() == null) return;
            float gameObjectNameWidth = GUI.skin.label.CalcSize(new GUIContent(go.name)).x;

            DrawComponents(go, selectionRect, gameObjectNameWidth);
            //Debug.Log(go.name);
            if (go.transform.parent == null)
            {
                _root = go;
            }
            else
            {
                // Component safeAreaLimiter = null; //go.transform.parent.GetComponent<SafeAreaLimiter>();
                if (go.GetComponent<AspectRatioFitter>())
                {
                    EditorGUI.DrawRect(new Rect(selectionRect)
                    {
                        yMin = selectionRect.yMin + 1,
                        yMax = selectionRect.yMax - 1,
                        xMin = selectionRect.xMax,
                        xMax = selectionRect.xMax + 20
                    }, new Color(0, 0, 0, 0.4f));
                    EditorGUI.DrawRect(new Rect(selectionRect)
                    {
                        yMin = selectionRect.yMin + 4,
                        yMax = selectionRect.yMax - 4,
                        xMin = selectionRect.xMax + 4,
                        xMax = selectionRect.xMax + 13
                    }, new Color(1, 1, 1, 0.4f));
                }
            }

            if (_treeViewDirty) CreateInjectedCache();
            var goId = go.GetInstanceID();
            var InjectPaths = ReferenceView.InjectPaths;
            var refCount = InjectPaths.TryGetValue(instanceId, out var value) ? value : 0;
            if (InjectPaths.ContainsKey(goId)) //*表示子级绑定了变量; ✫表示本层绑定了变量;*✫表示前两者都有;年代久远已经忘记实现细节:<
            {
                // Debug.Log($"---{instanceId} {goId} {go.name} {InjectPaths[instanceId]}");
                var num = InjectPaths[goId];
                var contain = num % 1000 != 0 ? "*" : "";
                var str = num < 1000 ? "*" : contain + "✫";
                Rect rr = new Rect(selectionRect)
                {
                    xMin = selectionRect.xMin - 4,
                    yMin = selectionRect.yMin - 4, width = 20
                };
                GUI.Label(rr, str);
            }
        }

        private static void CreateInjectedCache()
        {
            if (!_treeViewDirty || _root == null) return;

            InjectPaths.Clear();
            _treeViewDirty = false;
            var luaBehaviours = PrefabStageUtility.GetCurrentPrefabStage() == null
                ? Object.FindObjectsOfType<MonoBehaviour>(true).OfType<IAutoBindable>().ToArray()
                : _root.GetComponentsInChildren<IAutoBindable>(true);


            // _hasLuaBehavMissing = false;
            foreach (var behaviour in luaBehaviours)
            {
                var so = new SerializedObject(behaviour as Component);
                so.Update();
                var prop = so.GetIterator();
                var enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = false;
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (prop.name.StartsWith("c_"))
                        {
                            var com = prop.objectReferenceValue as Component;
                            var injectedGo = com.gameObject;
                            var id = injectedGo.GetInstanceID();
                            if (!InjectPaths.ContainsKey(id))
                                InjectPaths[id] = 0;
                            InjectPaths[id] = InjectPaths[id] + 1000;
                            var parentTrans = injectedGo.transform.parent;
                            while (parentTrans)
                            {
                                var pid = parentTrans.gameObject.GetInstanceID();
                                if (!InjectPaths.ContainsKey(pid))
                                    InjectPaths[pid] = 0;
                                InjectPaths[pid] += 1;
                                parentTrans = parentTrans.parent;
                            }
                        }
                    }
                }
            }
        }
    }
}