﻿using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public class MaterialCleaner
    {
        //获取shader中所有的宏
        public static bool GetShaderKeywords(Shader target, out string[] global, out string[] local)
        {
            try
            {
                MethodInfo globalKeywords = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                global = (string[]) globalKeywords.Invoke(null, new object[] {target});
                MethodInfo localKeywords = typeof(ShaderUtil).GetMethod("GetShaderLocalKeywords",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                local = (string[]) localKeywords.Invoke(null, new object[] {target});
                return true;
            }
            catch
            {
                global = local = null;
                return false;
            }
        }

        [MenuItem("Tools/重置着色器")]
        static void CleanMat()
        {
            var matList = AssetDatabase.FindAssets("t:Material", new[] {"Assets"});
            foreach (var s in matList)
            {
                var path = AssetDatabase.GUIDToAssetPath(s);
                Test222(path);
                Debug.Log(path);
            }
        }
        static void Test222(string matPath)
        {
            
            //这里替换成自己的材质，也可以深度遍历整个工程中的材质
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (GetShaderKeywords(m.shader, out var global, out var local))
            {
                HashSet<string> keywords = new HashSet<string>();
                foreach (var g in global)
                {
                    keywords.Add(g);
                }

                foreach (var l in local)
                {
                    keywords.Add(l);
                }

                //重置keywords
                List<string> resetKeywords = new List<string>(m.shaderKeywords);
                foreach (var item in m.shaderKeywords)
                {
                    if (!keywords.Contains(item))
                        resetKeywords.Remove(item);
                }

                m.shaderKeywords = resetKeywords.ToArray();
            }

            HashSet<string> property = new HashSet<string>();
            int count = m.shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                property.Add(m.shader.GetPropertyName(i));
            }

            SerializedObject o = new SerializedObject(m);
            SerializedProperty disabledShaderPasses = o.FindProperty("disabledShaderPasses");
            SerializedProperty SavedProperties = o.FindProperty("m_SavedProperties");
            SerializedProperty TexEnvs = SavedProperties.FindPropertyRelative("m_TexEnvs");
            SerializedProperty Floats = SavedProperties.FindPropertyRelative("m_Floats");
            SerializedProperty Colors = SavedProperties.FindPropertyRelative("m_Colors");
            //对比属性删除残留的属性
            for (int i = disabledShaderPasses.arraySize - 1; i >= 0; i--)
            {
                if (!property.Contains(disabledShaderPasses.GetArrayElementAtIndex(i).displayName))
                {
                    disabledShaderPasses.DeleteArrayElementAtIndex(i);
                }
            }

            for (int i = TexEnvs.arraySize - 1; i >= 0; i--)
            {
                if (!property.Contains(TexEnvs.GetArrayElementAtIndex(i).displayName))
                {
                    TexEnvs.DeleteArrayElementAtIndex(i);
                }
            }

            for (int i = Floats.arraySize - 1; i >= 0; i--)
            {
                if (!property.Contains(Floats.GetArrayElementAtIndex(i).displayName))
                {
                    Floats.DeleteArrayElementAtIndex(i);
                }
            }

            for (int i = Colors.arraySize - 1; i >= 0; i--)
            {
                if (!property.Contains(Colors.GetArrayElementAtIndex(i).displayName))
                {
                    Colors.DeleteArrayElementAtIndex(i);
                }
            }

            o.ApplyModifiedProperties();

            Debug.Log("Done!");
        }
    }
}