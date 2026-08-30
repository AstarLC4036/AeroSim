using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AeroSim.UI;

[CustomEditor(typeof(RadarHUDDrawer))]
public class RadarDrawerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        RadarHUDDrawer drawer = (RadarHUDDrawer)target;

        if(GUILayout.Button("Update Image"))
        {
            drawer.InitCanvas();
            drawer.UpdateCanvas();
        }
    }
}
