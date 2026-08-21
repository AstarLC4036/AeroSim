using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using AeroSim.AircraftModules;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(Missile))]
    public class MissileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Missile missile = (Missile)target;

            if (GUILayout.Button("Ignite"))
            {
                missile.Ignite();
            }
        }
    }
}
