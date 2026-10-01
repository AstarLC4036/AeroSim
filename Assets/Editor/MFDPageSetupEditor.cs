using AeroSim.Cockpit.MFD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(MFDPageSetup), true)]
    public class MFDPageSetupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();   // 其余字段照常

            EditorGUILayout.Separator();
            SerializedProperty list = serializedObject.FindProperty("pageList");
            if (list == null || !list.isArray) { serializedObject.ApplyModifiedProperties(); return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Marked List");
            EditorGUILayout.LabelField($"Page List ({list.arraySize})", EditorStyles.boldLabel);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty el = list.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                if (!el.isExpanded)
                {
                    el.isExpanded = EditorGUILayout.Foldout(el.isExpanded, $"[{i}] {ShortType(el.managedReferenceFullTypename)}", true);
                }
                if (GUILayout.Button("↑", GUILayout.Width(22)) && i > 0) list.MoveArrayElement(i, i - 1);
                if (GUILayout.Button("↓", GUILayout.Width(22)) && i < list.arraySize - 1) list.MoveArrayElement(i, i + 1);
                if (GUILayout.Button("−", GUILayout.Width(22))) { list.DeleteArrayElementAtIndex(i); break; }
                EditorGUILayout.EndHorizontal();
                if (el.isExpanded) EditorGUILayout.PropertyField(el, new GUIContent($"[{i}] {ShortType(el.managedReferenceFullTypename)}"), true);
                EditorGUILayout.EndVertical();
            }
            serializedObject.ApplyModifiedProperties();
        }

        static string ShortType(string full)   // "Assembly-CSharp AeroSim.Cockpit.MFD.CN.J10C.RadarPPIMMasterPage"
        {
            if (string.IsNullOrEmpty(full)) return "— 未指定类型 —";
            int sp = full.LastIndexOf(' ');
            string t = sp >= 0 ? full.Substring(sp + 1) : full;
            int dot = t.LastIndexOf('.');
            return dot >= 0 ? t.Substring(dot + 1) : t;
        }
    }
}
