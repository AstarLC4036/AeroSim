using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using AeroSim.Utils;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(ParticleFloatingOrigin))]
    public class ParticleFloatingOriginEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            ParticleFloatingOrigin particleFloatingOrigin = (ParticleFloatingOrigin)target;

            if(GUILayout.Button("Update Particles"))
            {
                particleFloatingOrigin.SearchAllParticles();
            }
        }
    }
}
