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
        }

        public List<ScreenProperty> mfdScreens = new List<ScreenProperty>();
        public List<MaterialPropertyBlock> mfdProperties = new List<MaterialPropertyBlock>();
        [SerializeField]
        public RadarBScopeMFD radarBScopeDrawer;
        [SerializeField]
        public RadarPPIMFD radarPPIDrawer;

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
            ScreenProperty radarDefineScreen = mfdScreens.Find(x => x.type == MFDType.RadarBScope || x.type == MFDType.RadarPPI);

            if (radarBScopeDrawer == null)
            {
                radarBScopeDrawer = new RadarBScopeMFD(new Vector2Int(1024, 1024), Color.black);
            }
            if (radarPPIDrawer == null)
            {
                radarBScopeDrawer = new RadarBScopeMFD(new Vector2Int(1024, 1024), Color.black);
            }

            // Set data override first
            //foreach (ScreenProperty mfd in mfdScreens)
            //{
            //    MFDDrawer drawer = GetMFDDrawer(mfd.type);
            //    if (drawer != null)
            //    {
            //        drawer.size = mfd.size;
            //        drawer.bgColor = mfd.bgColor;
            //    }
            //}

            radarBScopeDrawer.Init(parentAircraft);
            radarPPIDrawer.Init(parentAircraft);
        }

        private void InitMFDRender()
        {
            foreach (ScreenProperty mfd in mfdScreens)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                mfdProperties.Add(block);
                mfd.renderer.GetPropertyBlock(block, mfd.targetMaterialIndex);
                block.SetVector("_BaseMap_ST", new Vector4(mfd.tiling.x, mfd.tiling.y, mfd.offset.x, mfd.offset.y));
                block.SetTexture("_BaseMap", GetMFDTexture(mfd));
                mfd.renderer.SetPropertyBlock(block, mfd.targetMaterialIndex);
            }
        }

        private MFDDrawer GetMFDDrawer(MFDType type)
        {
            switch (type)
            {
                case (MFDType.RadarBScope):
                    return radarBScopeDrawer;
                case (MFDType.RadarPPI):
                    return radarPPIDrawer;
                default :
                    return null;
            }
        }

        private RenderTexture GetMFDTexture(ScreenProperty display)
        {
            MFDType type = display.type;
            switch (type)
            {
                case (MFDType.None):
                    RenderTexture newTex = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32)
                    {
                        enableRandomWrite = true,
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Point
                    };
                    newTex.Create();
                    return newTex;
                default :
                    return GetMFDDrawer(type).canvasTexture;
            }
        }

        public void Update()
        {
            if (parentAircraft.isControlling)
            {
                radarBScopeDrawer.UpdateCanvas();
                radarPPIDrawer.UpdateCanvas();
            }
        }

        private void OnApplicationQuit()
        {
            if (parentAircraft.isControlling)
            {
                radarBScopeDrawer.Dispose();
                radarPPIDrawer.Dispose();
            }
        }
    }
}