using AeroSim.AeroPhysics;
using AeroSim.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AeroSim.AircraftModules
{
    public class MFDDisplays : MonoBehaviour
    {
        [Serializable]
        public class ScreenProperty
        {
            [Header("Base")]
            public Renderer renderer;
            public int targetMaterialIndex;
            public Vector2 tiling;
            public Vector2 offset;
            public MFDType type;
            [Header("Property Override")]
            public Vector2Int size;
            public Color32 bgColor;
        }

        public Canvas displayCarrier;
        public List<ScreenProperty> mfdScreens = new List<ScreenProperty>();
        public List<MaterialPropertyBlock> mfdProperties = new List<MaterialPropertyBlock>();
        [SerializeField]
        public RadarMFD radarDrawer;

        public RawImage radarDisplay;
        public RawImage statusDisplay;
        //public Texture2D testTex;

        private Aircraft parentAircraft;

        public void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;

            if (aircraft.isControlling)
            {
                InitDrawer();
                InitMFDRender();
            }
        }

        private void InitDrawer()
        {
            radarDrawer = new RadarMFD();

            // Set data override first
            foreach (ScreenProperty mfd in mfdScreens)
            {
                MFDDrawer drawer = GetMFDDrawer(mfd.type);
                if (drawer != null)
                {
                    drawer.size = mfd.size;
                    drawer.bgColor = mfd.bgColor;
                }
            }

            radarDrawer.Init(parentAircraft);
        }

        private void InitMFDRender()
        {
            foreach (ScreenProperty mfd in mfdScreens)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                mfdProperties.Add(block);
                mfd.renderer.GetPropertyBlock(block, mfd.targetMaterialIndex);
                block.SetVector("_BaseMap_ST", new Vector4(mfd.tiling.x, mfd.tiling.y, mfd.offset.x, mfd.offset.y));
                block.SetTexture("_BaseMap", GetMFDTexture(mfd.type));
                mfd.renderer.SetPropertyBlock(block, mfd.targetMaterialIndex);
            }
        }

        private MFDDrawer GetMFDDrawer(MFDType type)
        {
            switch (type)
            {
                case (MFDType.Radar):
                    return radarDrawer;
                default :
                    return null;
            }
        }

        private Texture2D GetMFDTexture(MFDType type)
        {
            switch (type)
            {
                case (MFDType.None):
                    Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    tex.SetPixel(0, 0, new Color(25, 25, 25, 255));
                    return tex;
                default :
                    return GetMFDDrawer(type).CanvasTexture;
            }
        }

        public void Update()
        {
            if(parentAircraft.isControlling)
                radarDrawer.UpdateCanvas();
        }
    }
}