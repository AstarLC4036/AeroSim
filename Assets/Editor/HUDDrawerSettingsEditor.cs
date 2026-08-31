using AeroSim.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(MFDGraphicHelperSettings))]
    public class MFDDDrawerSettingsEditor : Editor
    {
        private ComputeShader m_shader;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MFDGraphicHelperSettings helper = (MFDGraphicHelperSettings)target;

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Debugging");
            m_shader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", m_shader, typeof(ComputeShader), true);
            if(GUILayout.Button("Apply"))
            {
                //MFDGraphicHelperSettings.MfdShader = m_shader;
            }
        }
    }
}
