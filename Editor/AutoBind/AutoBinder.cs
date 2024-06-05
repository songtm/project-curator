using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SFrame;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_2021_1_OR_NEWER
    using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;

#endif

// ReSharper disable IdentifierTypo

namespace AutoBind
{
    public class AutoBinder : EditorWindow
    {
        [MenuItem("Tools/AutoBinder")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            AutoBinder window = (AutoBinder) EditorWindow.GetWindow(typeof(AutoBinder));
            window.Show();
        }

        private string _objNameFormat = "name_{0}";
        private GameObject _selGo;
        private MonoBehaviour _luaBehaviour;
        private Vector2 _scrollPos;
        private bool _uiEnabled = true;
        private Dictionary<Component, string> _bindDicPre;
        private Dictionary<Component, string> _bindDicDst;

        [SerializeField] private List<string> _waitBindNames;
        [SerializeField] private List<Component> _waitBindComs;
        [SerializeField] private bool _needSaveInjection;

        private void OnSelectionChange()
        {
            UpdateSelGo();
            Repaint();
        }

        private void OnEnable()
        {
            CheckBindLink();

            UpdateSelGo();
        }

        private void CheckBindLink()
        {
            if (_luaBehaviour != null)
            {
                var dic = new Dictionary<string, Component>();
                for (var i = 0; i < _waitBindComs.Count; i++)
                {
                    dic[_waitBindNames[i]] = _waitBindComs[i];
                }

                var so = new SerializedObject(_luaBehaviour);
                so.Update();
                var prop = so.GetIterator();
                var enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (dic.ContainsKey(prop.name))
                        {
                            prop.objectReferenceValue = dic[prop.name];
                            Debug.Log($"[AutoBinder]Link {prop.name} -> {prop.objectReferenceValue}");
                        }
                    }

                    enterChildren = false;
                }

                so.ApplyModifiedProperties();
            }

            _luaBehaviour = null;
            _waitBindComs?.Clear();
            _waitBindNames?.Clear();
        }

        private void UpdateSelGo(bool calcCommonBehav = false)
        {
            _selGo = Selection.objects.Length == 1 ? Selection.activeGameObject : null;

            var behav = FindNearestBehav(_selGo);
            if (calcCommonBehav)
            {
                behav = null;
                foreach (var go in Selection.gameObjects)
                {
                    var curBehav = FindNearestBehav(go);
                    if (curBehav == null)
                    {
                        behav = null;
                        break;
                    }

                    if (behav == null)
                    {
                        behav = curBehav;
                    }
                    else if (curBehav != behav)
                    {
                        behav = null;
                        break;
                    }
                }

                if (behav == null) CustomEditorUtil.ShowEditorTip("no same container", BuildInWinType.SceneView);
            }

            // if (_luaBehaviour != behav)
            if (_luaBehaviour != behav && behav != null) //todo check this 防止切换空白时也提示(下面的保存)
            {
                if (_needSaveInjection && _bindDicDst != null)
                {
                    if (EditorUtility.DisplayDialog("", "切换绑定容器, 是否保存?", "保存", "忽略"))
                    {
                        SaveCurInjection(true);
                    }

                    _needSaveInjection = false;
                }

                _luaBehaviour = behav;
                _bindDicPre = new Dictionary<Component, string>();
                _bindDicDst = new Dictionary<Component, string>();

                if (behav != null)
                {
                    _bindDicPre = ParseBindDic(_luaBehaviour);
                    CopyDic(_bindDicPre, _bindDicDst);
                }
            }

            _uiEnabled = behav != null
                         && !PrefabUtility.IsPartOfAnyPrefab(behav)
                         && !_luaBehaviour.gameObject.name.EndsWith("(Clone)");
            // if (_uiEnabled)
            // {
            //     _bindDicPre = ParseBindDic(_luaBehaviour);
            //     CopyDic(_bindDicPre, _bindDicDst);
            // }
        }

        private void CopyDic(Dictionary<Component, string> src, Dictionary<Component, string> dst)
        {
            dst.Clear();
            foreach (var keyValuePair in src) dst[keyValuePair.Key] = keyValuePair.Value;
        }

        private static Dictionary<Component, string> ParseBindDic(MonoBehaviour behaviour)
        {
            var bindDic = new Dictionary<Component, string>();
            var so = new SerializedObject(behaviour);
            so.Update();
            var prop = so.GetIterator();
            var enterChildren = true;
            while (prop.Next(enterChildren))
            {
                var name = prop.name;
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var val = prop.objectReferenceValue;
                    if (name.StartsWith("c_") && val != null && val is Component component)
                    {
                        bindDic[component] = name;
                        // Debug.Log($"ref {name}");
                    }
                }

                enterChildren = false;
            }

            return bindDic;
        }

        private void OnDisable()
        {
        }

        private static string MakeBindName(string objName, Type com)
        {
            if (objName.StartsWith("#")) objName = objName.Substring(1);
            var comName = ComNameDic.ContainsKey(com.Name) ? ComNameDic[com.Name] : com.Name;

            comName = char.ToLower(comName[0]) + comName.Substring(1);
            foreach (var name in ComNameDic.Keys)
            {
                if (objName.ToLower().StartsWith(name.ToLower()))
                {
                    objName = objName.Substring(name.Length);
                }
            }


            var obj = objName.Length > 1 ? char.ToUpper(objName[0]) + objName.Substring(1) : objName;
            obj = obj.Replace(" ", "");
            var result = string.Concat(obj.Select((x, i) =>
            {
                if (i > 0)
                {
                    if (x == '_')
                        return "";

                    return obj[i - 1] == '_' ? char.ToUpper(x).ToString() : x.ToString();
                }

                return x.ToString();
            }));
            return "c_" + comName + result;
        }

        private void OnClickComponent(Component component, string bindName)
        {
            if (Event.current.button == 1) //right
            {
                // SaveCurInjection();
                // OpenLuaFile(_luaBehaviour, component, _indexKeyDic[component]);
                CustomEditorUtil.OpenScriptFile(_luaBehaviour, bindName);
            }
            else if (Event.current.button == 2) //mid
            {
                if (!string.IsNullOrEmpty(_bindDicDst[component]))
                {
                    GUIUtility.systemCopyBuffer = _bindDicDst[component];
                    ShowNotification(new GUIContent("copied:" + _bindDicDst[component]), 0.5);
                }
            }
            else if (Event.current.button == 0) //left
            {
                GUI.FocusControl(null);
                var newName = MakeBindName(_selGo.name, component.GetType());
                SetDstBindName(component, newName);
                // SaveCurInjection();
            }
        }

        private void SetDstBindName(Component com, string name)
        {
            if (_bindDicDst.TryGetValue(com, out var val) && val == name) return;

            if (string.IsNullOrEmpty(val) && string.IsNullOrEmpty(name)) return;

            _bindDicDst[com] = name;
            _needSaveInjection = true;
        }

        private void SaveCurInjection(bool force = false)
        {
            _needSaveInjection = false;
            if (!_uiEnabled && !force) return;
            var script = MonoScript.FromMonoBehaviour(_luaBehaviour);
            var path = AssetDatabase.GetAssetPath(script);

            var content = File.ReadAllText(path);
            if (!content.Contains("ReSharper disable InconsistentNaming"))
                content = $"// ReSharper disable InconsistentNaming{Environment.NewLine}" + content;
            foreach (var keyValuePair in _bindDicDst)
            {
                var com = keyValuePair.Key;
                var name = keyValuePair.Value;
                if (_bindDicPre.ContainsKey(com))
                {
                    if (_bindDicPre[com] != name)
                    {
                        var match = Regex.Match(content,
                            @$"([\s\S]*)(\[SerializeField\]\s+private\s+{com.GetType().Name}\s+{_bindDicPre[com]};)([\s\S]*)");
                        if (match.Success)
                        {
                            if (string.IsNullOrEmpty(name)) //dell
                            {
                                content = match.Groups[1].ToString() + match.Groups[3];
                                // var so = new SerializedObject(_luaBehaviour);
                                // so.Update();
                                // var prop = so.FindProperty(_bindDicPre[com]);
                                // prop.DeleteCommand();
                            }
                            else //change
                            {
                                var line = $"[SerializeField] private {com.GetType().Name} {name};";
                                content = match.Groups[1].ToString() + line + match.Groups[3];
                                _waitBindComs.Add(com);
                                _waitBindNames.Add(name);
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(name)) //new
                {
                    var clsName = _luaBehaviour.name;
                    var match = Regex.Match(content, $@"([\s\S]*)({clsName}.*[\s\S].*?\{{)([\s\S]*)");
                    if (match.Success)
                    {
                        var line = $"{Environment.NewLine}\t[SerializeField] private {com.GetType().Name} {name};";
                        content = match.Groups[1].ToString() + match.Groups[2] + line + match.Groups[3];
                        _waitBindComs.Add(com);
                        _waitBindNames.Add(name);
                    }
                }
            }

            File.WriteAllText(path, content);
            AssetDatabase.Refresh();
            InejctChanged?.Invoke();
            // CopyDic(_bindDicDst, _bindDicPre);
        }

        private void OnGUIHeader()
        {
            if (_luaBehaviour == null) return;
            GUILayout.BeginHorizontal();
            GUI.enabled = false;
#pragma warning disable 618
            EditorGUILayout.ObjectField(_luaBehaviour, _luaBehaviour.GetType());
#pragma warning restore 618
            GUI.enabled = true;
            if (GUILayout.Button("打开容器", EditorStyles.toolbarButton, GUILayout.MaxWidth(60)))
            {
                CustomEditorUtil.OpenScriptFile(_luaBehaviour);
            }

            GUI.enabled = _needSaveInjection;
            if (GUILayout.Button("生成绑定", EditorStyles.toolbarButton, GUILayout.MaxWidth(60)))
            {
                SaveCurInjection();
            }

            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        private void DrawMultiSelGui()
        {
            EditorGUILayout.Space();

            if (Selection.transforms.Length == 2 && GUILayout.Button("复制绑定", GUILayout.MaxWidth(60)))
            {
                CopyUIBinding.copyLuaBindingMenu();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 80;
            _objNameFormat = EditorGUILayout.TextField("batch rename", _objNameFormat, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("<-", GUILayout.MaxWidth(25)))
            {
                _objNameFormat = $"{Selection.objects[0].name}{{0}}";
            }

            if (GUILayout.Button("rename", GUILayout.MaxWidth(60)))
            {
                Undo.RecordObjects(Selection.objects, "rename objs");
                for (var i = 0; i < Selection.objects.Length; i++)
                {
                    var obj = Selection.objects[i];
                    obj.name = string.Format(_objNameFormat, i + 1);
                }
            }

            EditorGUIUtility.labelWidth = 0;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawUtilsGui()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 50;
            _guid = EditorGUILayout.TextField("guid:", _guid);
            if (GUILayout.Button("search", GUILayout.MaxWidth(60)))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(_guid.Trim());
                if (string.IsNullOrEmpty(assetPath))
                {
                    CustomEditorUtil.ShowEditorTip("can't find", BuildInWinType.SceneHierarchyWindow);
                }
                else
                {
                    CustomEditorUtil.VisibleBuiltinWindow(BuildInWinType.ProjectBrowser);
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                }
            }

            EditorGUIUtility.labelWidth = 0;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("UnloadUnusedAssets"))
            {
                Resources.UnloadUnusedAssets();
            }

            if (GUILayout.Button("GC"))
            {
                GC.Collect();
            }

            if (GUILayout.Button("CurSelObj"))
            {
                var sel = EventSystem.current.currentSelectedGameObject;

                if (sel != null)
                {
                    Debug.Log($"sel obj:{sel.name}");
                    EditorGUIUtility.PingObject(sel);
                    Selection.activeGameObject = sel;
                }
                else
                {
                    ShowNotification(new GUIContent("sel object:null"));
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        void OnGUI()
        {
            if (_selGo == null)
            {
                if (Selection.transforms.Length > 1)
                {
                    DrawMultiSelGui();
                }
                else
                {
                    DrawUtilsGui();
                }

                return;
            }

            OnGUIHeader();
            if (_luaBehaviour == null) return;
            GUI.enabled = _uiEnabled;
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var components = _selGo.GetComponents<Component>();
            var bindNameCount = new Dictionary<string, int>();
            foreach (var p in _bindDicDst)
            {
                if (string.IsNullOrEmpty(p.Value)) continue;
                if (bindNameCount.ContainsKey(p.Value))
                    bindNameCount[p.Value]++;
                else
                    bindNameCount[p.Value] = 0;
            }

            foreach (var component in components)
            {
                EditorGUILayout.BeginHorizontal();
                // GUILayout.Label(component.GetType().Name);
                _bindDicDst.TryGetValue(component, out var bindName);
                var name = EditorGUILayout.TextField(component.GetType().Name, bindName ?? "");
                SetDstBindName(component, name);

                var dup = bindNameCount.TryGetValue(name, out var count);
                if (count > 0)
                {
                    var rect = GUILayoutUtility.GetLastRect();
                    EditorGUI.DrawRect(new Rect(rect.x, rect.center.y, rect.width, 2), Color.red);
                }

                if (GUILayout.Button("<-", EditorStyles.toolbarButton, GUILayout.MaxWidth(20)))
                {
                    OnClickComponent(component, name);
                }

                if (Event.current.type == EventType.KeyUp)
                {
                    if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    {
                        // Debug.Log("save injection");
                        Event.current.Use();
                        // SaveCurInjection();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            GUI.enabled = true;
        }


        public static MonoBehaviour FindNearestBehav(GameObject go)
        {
            MonoBehaviour behav = null;
            if (go)
            {
                var cur = go.transform;
                var findFirst = cur.GetComponent<IAutoBindable>() == null || cur.parent == null;
                var goPath = "";
                while (cur) //找距离最近的LuaBehaviourEx组件
                {
                    goPath = cur.name + "/" + goPath;
                    // ReSharper disable once AssignmentInConditionalExpression
                    if (behav = cur.GetComponent<IAutoBindable>() as MonoBehaviour)
                    {
                        if (findFirst) break;
                        if (cur.parent.GetComponentInParent<IAutoBindable>() == null) break;
                        findFirst = true;
                    }

                    cur = cur.parent;
                }
            }

            return behav;
        }


        [MenuItem("CONTEXT/Component/批量绑定Lua变量", false, 20000)]
        private static void BatchBind(MenuCommand cmd)
        {
            var com = cmd.context as Component;
            if (com == null) return;
            AutoBinder window = (AutoBinder) EditorWindow.GetWindow(typeof(AutoBinder));
            window.Show(true);

            window.UpdateSelGo(true);
            if (window._luaBehaviour == null) return;

            var newName = MakeBindName(com.gameObject.name, com.GetType());

            window.SetDstBindName(com, newName);

            GUI.FocusControl(null);
        }

        [MenuItem("CONTEXT/Component/批量绑定Lua变量", true)]
        private static bool BatchBindShow() =>
            PrefabStageUtility.GetCurrentPrefabStage() != null && Selection.gameObjects.Length > 1;

        private static readonly Dictionary<string, string> ComNameDic = new Dictionary<string, string>
        {
            {"ObservableText", "txt"},
            {"GridView", "gv"},
            {"ButtonOverride", "btn"},
            {"SliderOverride", "sld"},
            {"RaySliderOverride", "sld"},
            {"ToggleOverride", "tog"},
            {"RawImage", "rmg"},
            {"RoundRawImage", "rmg"},
            {"BindableBehaviour", "sub"},
            {"Button", "btn"},
            {"Text", "txt"},
            {"SpriteRenderer", "spr"},
            {"RectTransform", "trs"},
            {"Transform", "trs"},
            {"Slider", "sld"},
            {"Toggle", "tog"},
            {"Dropdown", "drop"},
            {"InputField", "edit"},
            {"Image", "img"},
            {"Scrollbar", "sbar"},

            {"MeshFilter", "msflt"},
            {"MeshRenderer", "msr"},
            {"ParticleSystem", "pts"},
            {"SkinnedMeshRenderer", "smsr"},
            {"Renderer", "render"},
            {"Light", "light"},
            {"Canvas", "canvas"},
            {"ScrollRect", "srect"},
            {"TextMeshPro", "txt"},
            {"TextMeshProUGUI", "txt"},
            {"TMP_Text", "txt"},
            {"SkeletonAnimation", "skAni"},
            {"SkeletonRenderer", "skr"},
            {"Skeleton", "sk"},
            {"LoopHorizontalScrollRect", "scl"},
            {"LoopVerticalScrollRect", "scl"},
            {"SortingGroup", "sg"},
            {"Animator", "ant"},
        };

        private string _guid;

        public static event Action InejctChanged;
    }
}