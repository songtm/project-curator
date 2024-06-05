using System;
using System.Collections.Generic;
using SFrame;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AutoBind
{
    public class CopyUIBinding
    {
        // [MenuItem("CONTEXT/Component/批量绑定Lua变量", false, 20000)]
        // public static void BatchBind(MenuCommand cmd)
        // {
        //     var com = cmd.context as Component;
        //     if (com == null) return;
        //     var behav = FindNearestBehav(com.gameObject);
        //     if (behav == null) return;
        //     var newName = MakeBindName(com.gameObject.name, com.GetType());
        //     var indexDic = new Dictionary<Component, string>();
        //     indexDic[com] = newName;
        //     // _indexKeyDic[component] = newName;
        //     SaveOneInjection(behav, new SerializedObject(behav), null, indexDic);
        //     GUI.FocusControl(null);
        //     behav.GenLuaTip(_uiTipPath);
        // }

        [MenuItem("Window/-/copy lua bind")]
        public static void copyLuaBindingMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("复制绑定"), false, () => { copyLuaBinding(true); });
            menu.AddItem(new GUIContent("复制绑定+位置"), false, () => { copyLuaBinding(true); });
            menu.AddItem(new GUIContent("复制绑定+位置-父子"), false, () => { copyLuaBinding(true, true); });
            menu.AddItem(new GUIContent("复制绑定+位置-父子-rectSize"), false,
                () => { copyLuaBinding(true, true, true); });


            menu.AddSeparator("");
            menu.AddItem(new GUIContent("复制-位置"), false, () => { SyncTransform(true); });
            menu.AddItem(new GUIContent("复制-位置-父子"), false, () => { SyncTransform(true, true); });
            menu.AddItem(new GUIContent("复制-位置-父子-rectSize"), false, () => { SyncTransform(true, true, true); });
            menu.ShowAsContext();
        }

        private static void SyncTransform(bool copyPos = false, bool syncParent = false, bool copyAnchorAndSize = false)
        {
            var objs = Selection.objects;
            if (objs.Length != 2 || !(objs[0] is GameObject) || !(objs[1] is GameObject))
            {
                CustomEditorUtil.ShowEditorTip("need 2 gameobject in hierachy", BuildInWinType.SceneHierarchyWindow);
                return;
            }

            copyPosAndSize(objs[0] as GameObject, objs[1] as GameObject, syncParent, copyPos, copyAnchorAndSize);
        }


        private static bool UpdateReference(Component behaviour, Component comOld, Component comNew, bool syncName,
            bool log = true)
        {
            if (comOld == comNew) return false;

            var so = new SerializedObject(behaviour);
            so.Update();

            var prop = so.GetIterator();
            var enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                    prop.objectReferenceValue is Component reference && reference == comOld)
                {
                    prop.objectReferenceValue = comNew;
                    if (syncName)
                    {
                        Undo.RecordObject(comNew.gameObject, "rename");
                        // Undo.RegisterCompleteObjectUndo(comNew.gameObject, "rename");
                        comNew.gameObject.name = comOld.gameObject.name;
                        if (log) Debug.Log($"更新绑定:{behaviour.name}.{prop.name}");
                    }

                    so.ApplyModifiedProperties();
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<Component, string> GetRefDic(Component behaviour)
        {
            var so = new SerializedObject(behaviour);
            so.Update();
            var name2Com = new Dictionary<string, Component>();
            var com2name = new Dictionary<Component, string>();

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
                        if (com == null) continue;
                        name2Com[prop.name] = com;
                        com2name[com] = prop.name;
                    }
                }
            }

            return com2name;
        }

        public static void copyLuaBinding(bool copyPos = false, bool syncParent = false, bool copyAnchorAndSize = false)
        {
            var rename = true;
            var objs = Selection.objects;
            if (objs.Length != 2 || !(objs[0] is GameObject) || !(objs[1] is GameObject))
            {
                CustomEditorUtil.ShowEditorTip("need 2 gameobject in hierachy", BuildInWinType.SceneHierarchyWindow);
                return;
            }

            var obj0 = objs[0] as GameObject;
            var obj1 = objs[1] as GameObject;
            if (obj0.GetComponent<IAutoBindable>() != null || obj1.GetComponent<IAutoBindable>() != null)
            {
                CustomEditorUtil.ShowEditorTip("can't copy nested prefab", BuildInWinType.SceneHierarchyWindow);
                return;
            }

            var behav0 = AutoBinder.FindNearestBehav(obj0);
            var behav1 = AutoBinder.FindNearestBehav(obj1);
            if (behav0 == null || behav1 == null)
            {
                CustomEditorUtil.ShowEditorTip("need LuaBehaviour", BuildInWinType.SceneHierarchyWindow);
                return;
            }

            if (behav0 != behav1)
            {
                CustomEditorUtil.ShowEditorTip("need under same LuaBehaviour", BuildInWinType.SceneHierarchyWindow);
                return;
            }

            copyPosAndSize(obj0, obj1, syncParent, copyPos, copyAnchorAndSize);


            Debug.Log($"更新绑定开始----------------------");
            if (obj0.transform.childCount > 0 && obj1.transform.childCount == obj0.transform.childCount)
            {
                var sameStructure = true;
                var allcom0 = obj0.GetComponentsInChildren<Component>(true);
                var allcom1 = obj1.GetComponentsInChildren<Component>(true);
                if (allcom0.Length == allcom1.Length)
                {
                    for (var i = 0; i < allcom0.Length; i++)
                    {
                        if (allcom0[i].GetType() != allcom1[i].GetType())
                        {
                            sameStructure = false;
                            break;
                        }
                    }

                    if (sameStructure && EditorUtility.DisplayDialog("Move tree stars",
                        "detect same tree structure, will you move lua binding of all children?",
                        "do move self and all children!", "取消"))
                    {
                        for (var i = 0; i < allcom0.Length; i++)
                        {
                            UpdateReference(behav0, allcom0[i], allcom1[i], rename);
                        }

                        return;
                    }
                }
            }

            //只处理当前层级
            var coms0 = obj0.GetComponents<Component>();
            var coms1 = obj1.GetComponents<Component>();
            foreach (var com0 in coms0)
            {
                int count = 0;
                Component sameCom = null;
                foreach (var com1 in coms1)
                {
                    if (com1.GetType() == com0.GetType())
                    {
                        count++;
                        sameCom = com1;
                    }
                }

                if (count == 1)
                {
                    UpdateReference(behav0, com0, sameCom, rename);
                }
            }

            //button特殊处理
            if (obj0.GetComponent<Button>() && obj1.GetComponent<Button>())
            {
                var texts0 = obj0.GetComponentsInChildren<Text>(true);
                var texts1 = obj1.GetComponentsInChildren<Text>(true);
                if (texts0.Length == 1 && texts0.Length == texts1.Length)
                {
                    UpdateReference(behav0, texts0[0], texts1[0], rename);
                }
            }

            Debug.Log($"更新绑定结束----------------------");
        }

        // Undo.DestroyObjectImmediate(obj0);       
        public static void copyPosAndSize(GameObject obj0, GameObject obj1, bool setSameParent, bool copyPos,
            bool copyAnchorAndSize)
        {
            Undo.RecordObject(obj1.transform, "copyPosAndSize");
            // Undo.RegisterCompleteObjectUndo(comNew.gameObject, "rename");

            if (setSameParent)
            {
                obj1.transform.SetParent(obj0.transform.parent, true);
                obj1.transform.SetSiblingIndex(obj0.transform.GetSiblingIndex() + 1);
            }

            var rectTransform0 = obj0.GetComponent<RectTransform>();
            var rectTransform1 = obj1.GetComponent<RectTransform>();

            if (rectTransform0 && copyAnchorAndSize)
            {
                rectTransform1.anchorMin = rectTransform0.anchorMin;
                rectTransform1.anchorMax = rectTransform0.anchorMax;
                rectTransform1.sizeDelta = rectTransform0.sizeDelta;
                rectTransform1.pivot = rectTransform0.pivot;
                rectTransform1.localScale = rectTransform0.localScale;
            }

            if (copyPos)
            {
                if (rectTransform0) rectTransform1.anchoredPosition = rectTransform0.anchoredPosition;
                obj1.transform.position = obj0.transform.position;
            }
        }
    }
}