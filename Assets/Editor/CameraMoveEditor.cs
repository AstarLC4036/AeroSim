using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using AeroSim.InputSystem;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(CameraController))]
    public class CameraMoveEditor : Editor
    {
        public int currentIndex = 0;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Separator();

            CameraController component = (CameraController)target;

            currentIndex = EditorGUILayout.IntField("View Index", currentIndex);
            if(GUILayout.Button("Apply View"))
            {
                if (currentIndex < component.views.Length)
                    component.SetView(currentIndex);
                else
                    Debug.LogWarning($"Index out of range, max index: {component.views.Length - 1}");
            }
        }
    }
}
