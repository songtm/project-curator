using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SFrame;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace AutoBind
{
    public class ReferenceView : EditorWindow
    {
        [MenuItem("Tools/ReferenceView", false, 200)]
        static void Init() => GetWindow<ReferenceView>("ReferenceView").Show();

        public static readonly Dictionary<int, int> InjectPaths = new Dictionary<int, int>();
        public static bool OverrideInjectPaths = false;
        public static readonly HashSet<Component> InjectedComs = new HashSet<Component>();

        private readonly Dictionary<int, List<RefInfo>> _infoDic = new Dictionary<int, List<RefInfo>>();
        private Dictionary<MonoBehaviour, string> _mono2Path = new Dictionary<MonoBehaviour, string>();

        private Dictionary<string, List<Tuple<string, string>>> _scriptRefPaths =
            new Dictionary<string, List<Tuple<string, string>>>();

        private readonly GUIStyle _style = new GUIStyle();
        private Regex _regex = new Regex(@"(.*)\.Find\(""(.*?)""\).*");
        [SerializeField] private bool textIndex;
        [SerializeField] private bool textIndexPackageDir;
        [SerializeField] private bool showSearchVar;
        [SerializeField] private string customSearchDir;
        private Vector2 _scrollPos;
        private bool locked = false;
        private GameObject _lockedGo;
        private string _searchVarName;
        private List<RefInfo> _searchVarRefInfos = new List<RefInfo>();
        private bool _dirty = true;

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnBecameInvisible()
        {
            OverrideInjectPaths = false;
        }

        private void OnBecameVisible()
        {
            OverrideInjectPaths = true;
        }

        private void Cleanup()
        {
            InjectedComs.Clear();
            InjectPaths.Clear();
            _mono2Path.Clear();
            _infoDic.Clear();
        }

        private void OnEnable()
        {
            _dirty = true;
            _style.richText = true;
            EditorApplication.hierarchyChanged += EditorApplicationOnhierarchyChanged;
        }

        private void OnDisable()
        {
            Cleanup();
            EditorApplication.hierarchyChanged -= EditorApplicationOnhierarchyChanged;
        }

        private void EditorApplicationOnhierarchyChanged()
        {
            _dirty = true;
        }

        private void Lock()
        {
            locked = true;
            _lockedGo = Selection.activeGameObject;
        }

        private void OnGUISearchVarHeader()
        {
            void Search()
            {
                if (_dirty) FindReference();
                _searchVarRefInfos.Clear();
                foreach (var pair in _infoDic)
                {
                    foreach (var info in pair.Value)
                    {
                        if (info.varName.ToLower().Contains(_searchVarName.ToLower())) _searchVarRefInfos.Add(info);
                    }
                }

                GUI.FocusControl(null);
            }

            GUILayout.BeginHorizontal();
            _searchVarName = EditorGUILayout.TextField(_searchVarName);
            if (GUILayout.Button("ClearVar"))
            {
                _searchVarName = "";
                GUI.FocusControl(null);
                _searchVarRefInfos.Clear();
            }

            if (GUILayout.Button("SearchVar"))
            {
                Search();
            }

            if (GUILayout.Button("SearchClipVar"))
            {
                _searchVarName = EditorGUIUtility.systemCopyBuffer;
                Search();
            }

            GUILayout.EndHorizontal();
        }

        private void OnGUISearchVar()
        {
            foreach (var refInfo in _searchVarRefInfos)
            {
                DrawOneRefInfo(refInfo, "[搜索]");
            }

            EditorGUILayout.LabelField("----------EndSearchVar-----------------");
            EditorGUILayout.Space();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 70;
            textIndex = EditorGUILayout.Toggle("IndexScript", textIndex);
            EditorGUIUtility.labelWidth = 75;
            textIndexPackageDir = EditorGUILayout.Toggle("IncPackages", textIndexPackageDir);
            // customSearchDir = EditorGUILayout.TextField(customSearchDir);
            EditorGUIUtility.labelWidth = 60;
            showSearchVar = EditorGUILayout.Toggle("SearchVar", showSearchVar);
            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.labelWidth = 30;
            locked = EditorGUILayout.Toggle("Lock", locked);
            if (EditorGUI.EndChangeCheck() && locked)
            {
                Lock();
            }

            if (!locked) _lockedGo = null;

            if (GUILayout.Button((_dirty ? "*" : "") + "ReBuild"))
            {
                FindReference();
                EditorApplication.RepaintHierarchyWindow();
            }

            EditorGUILayout.EndHorizontal();
            
            if (showSearchVar) OnGUISearchVarHeader();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (showSearchVar) OnGUISearchVar();
            OnGUISelGORefInfo();

            EditorGUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = 0;
        }

        private bool OnGUISelGORefInfo()
        {
            var selGo = Selection.objects.Length == 1 ? Selection.activeGameObject : null;
            if (_lockedGo != null) selGo = _lockedGo;

            if (selGo == null)
            {
                return true;
            }

            // EditorGUILayout.LabelField($"{selGo.name}的引用如下：");
            if (_infoDic.ContainsKey(selGo.GetInstanceID()))
            {
                foreach (var refInfo in _infoDic[selGo.GetInstanceID()])
                {
                    DrawOneRefInfo(refInfo);
                }
            }

            return false;
        }

        private void DrawOneRefInfo(RefInfo refInfo, string prefix = "")
        {
            var com = refInfo.ownerCom;
            if (com == null) return;
            var varType = refInfo.typeName;
            var varName = refInfo.varName;
            var label = $" {varName} -> <color=#0000ffff>{refInfo.refGo.name}:{varType}</color>";
            if (refInfo.fromTextSearch) label = varName;

            if (refInfo.ownerCom.gameObject == refInfo.refGo) label = "[自身]" + label;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.ObjectField(com, com.GetType(), true);
            if (GUILayout.Button(prefix + label, _style))
            {
                OnClickLabel(refInfo);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void OnClickLabel(RefInfo refInfo)
        {
            if (Event.current.button == 1) //right
            {
                if (refInfo.ownerCom is MonoBehaviour mono)
                {
                    var varName = refInfo.varName;
                    if (varName.Contains("[")) varName = varName.Split('[')[0];
                    if (refInfo.fromTextSearch)
                        CustomEditorUtil.OpenScriptFile(mono, varName);
                    else
                        CustomEditorUtil.OpenScriptFile(mono, @$"({refInfo.typeName}\s+{varName})\s*;",
                            @$"(\s+{varName})\s*;");
                    
                }
            }
            else if (Event.current.button == 0) //left
            {
                CustomEditorUtil.VisibleInInspector(refInfo.ownerCom, refInfo.displayName, true);
            }
            else if (Event.current.button == 2) //mid
            {
                if (!locked) Lock();
                CustomEditorUtil.VisibleInInspector(refInfo.ownerCom, refInfo.displayName, true);
            }
        }

        private void FindReferenceByTextMatch(MonoBehaviour behav)
        {
            if (_mono2Path.ContainsKey(behav)) return;
            var path = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behav));
            if (!path.EndsWith(".cs")) return;
            var fullName = behav.GetType().FullName;
            if (fullName != null && fullName.StartsWith("UnityEngine"))
            {
                return;
            }

            var search = path.StartsWith("Assets/") || textIndexPackageDir && path.StartsWith("Packages/");

            if (!search) return;

            if (!string.IsNullOrEmpty(customSearchDir))
            {
                search = path.StartsWith(customSearchDir);
            }

            _mono2Path[behav] = path;

            if (_scriptRefPaths.TryGetValue(path, out var refPaths))
            {
                foreach (var refPath in refPaths)
                {
                    var refTrans = behav.transform.Find(refPath.Item1); //只查找一层
                    if (refTrans != null)
                    {
                        var varName = refPath.Item2;
                        AddOneRef(refTrans.gameObject, new RefInfo
                        {
                            ownerCom = behav, typeName = "",
                            varName = varName,
                            displayName = null,
                            fromTextSearch = true,
                        });
                    }
                }
            }
            else
            {
                // Debug.Log($"search file: {path}");
                var txt = File.ReadAllText(path);
                _scriptRefPaths.Add(path, new List<Tuple<string, string>>());
                foreach (Match match in _regex.Matches(txt))
                {
                    var refPath = match.Groups[2].Value;
                    // Debug.Log(comPath);
                    var refTrans = behav.transform.Find(refPath); //只查找一层
                    if (refTrans != null)
                    {
                        var varName = match.Groups[0].Value.Trim();
                        AddOneRef(refTrans.gameObject, new RefInfo
                        {
                            ownerCom = behav, typeName = "",
                            varName = varName,
                            displayName = null,
                            fromTextSearch = true,
                        });
                        _scriptRefPaths[path].Add(new Tuple<string, string>(refPath, varName));
                    }
                }
            }
        }

        private void FindReference()
        {
            _dirty = false;
            Cleanup();
            EditorUtility.DisplayCancelableProgressBar("计算中", "", 0);
            var coms = PrefabStageUtility.GetCurrentPrefabStage() == null
                ? FindObjectsOfType<Component>(true)
                : PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot
                    .GetComponentsInChildren<Component>(true);

            for (int i = 0; i < coms.Length; i++)
            {
                var com = coms[i];
                if (com is Transform) continue;

                if (EditorUtility.DisplayCancelableProgressBar("计算中", $"{i}/{coms.Length} {com.GetType().FullName}",
                    (float) i / coms.Length))
                {
                    break;
                }

                if (com is MonoBehaviour m && textIndex) FindReferenceByTextMatch(m);

                var so = new SerializedObject(com);
                so.Update();
                var prop = so.GetIterator();
                var enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = false;
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        AddOnePropertyRef(prop, com);
                    }
                    else if (prop.isArray)
                    {
                        for (var i1 = 0; i1 < prop.arraySize; i1++)
                        {
                            var property = prop.GetArrayElementAtIndex(i1);
                            AddOnePropertyRef(property, com, $"{prop.name}[{i1}]");
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private void AddOnePropertyRef(SerializedProperty prop, Component ownerCom, string varName = null)
        {
            if (prop == null) return;
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                GameObject go = null;
                var typeName = "";
                if (prop.objectReferenceValue is GameObject g)
                {
                    go = g;
                    typeName = g.GetType().Name;
                }
                else if (prop.objectReferenceValue is Component c)
                {
                    go = c.gameObject;
                    typeName = c.GetType().Name;
                }

                if (go == null) return;
                AddOneRef(go, new RefInfo
                {
                    ownerCom = ownerCom, typeName = typeName, varName = varName ?? prop.name,
                    displayName = prop.displayName
                });
            }
        }

        private void AddOneRef(GameObject refGo, RefInfo refInfo)
        {
            var isUnityModule = (refInfo.ownerCom.GetType().Namespace ?? "").StartsWith("UnityEngine");

            if (refInfo.varName == "m_CorrespondingSourceObject") return; //特殊处理下吧！

            if (refInfo.ownerCom.gameObject == refGo && refInfo.varName == "m_GameObject")
                return; //每个组件都对自身GameObject有引用
            // if (refInfo.ownerCom is )
            if ((refGo.hideFlags & HideFlags.HideInInspector) != 0) return;
            
            var id = refGo.GetInstanceID();
            refInfo.refGo = refGo;
            if (!isUnityModule) InjectedComs.Add(refInfo.ownerCom);
            if (!InjectPaths.ContainsKey(id))
            {
                InjectPaths[id] = 0;
            }

            if (!_infoDic.ContainsKey(id))
                _infoDic[id] = new List<RefInfo>();
            _infoDic[id].Add(refInfo);
            InjectPaths[id] = InjectPaths[id] + 1000;
            var parentTrans = refGo.transform.parent;
            while (parentTrans)
            {
                var pid = parentTrans.gameObject.GetInstanceID();
                if (!InjectPaths.ContainsKey(pid))
                    InjectPaths[pid] = 0;
                InjectPaths[pid] += 1;
                parentTrans = parentTrans.parent;
            }
        }

        class RefInfo
        {
            public Component ownerCom { get; set; }
            public GameObject refGo { get; set; } //被引用的go
            public string typeName { get; set; }
            public string varName { get; set; }
            public string displayName { get; set; }
            public bool fromTextSearch { get; set; }
        }
    }
}