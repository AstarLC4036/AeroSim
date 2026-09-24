using AeroSim.AircraftModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(HydraulicMechSystem))]
    public class HydraulicMechSystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            HydraulicMechSystem system = (HydraulicMechSystem)target;

            if (GUILayout.Button("Play (Forward)"))
            {
                system.Play();
            }
            if (GUILayout.Button("Play (Backward)"))
            {
                system.Play(-1);
            }
            if (GUILayout.Button("Stop"))
            {
                system.Stop();
            }
            if (GUILayout.Button("Reset"))
            {
                system.ResetState();
            }
        }
    }
}
