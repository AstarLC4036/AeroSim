using AeroSim.Terrain;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AeroSim.CompEditor
{
    [CustomEditor(typeof(TerrainGenerator))]
    public class TerrainGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            TerrainGenerator generator = (TerrainGenerator)target;

            if(GUILayout.Button("Generate"))
            {
                generator.CreateMesh();
            }
        }
    }
}
