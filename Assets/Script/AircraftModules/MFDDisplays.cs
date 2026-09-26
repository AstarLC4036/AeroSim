using AeroSim.AeroPhysics;
using AeroSim.UI;
using System;
using System.Collections.Generic;
using TMPro;
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
        [SerializeField]
        public GimbalMFD gimbalDrawer;
        public TMP_FontAsset stdFont;

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

        /// <summary>
        /// Init all drawers
        /// </summary>
        private void InitDrawer()
        {
            ScreenProperty radarDefineScreen = mfdScreens.Find(x => x.type == MFDType.RadarBScope || x.type == MFDType.RadarPPI);

            if (radarBScopeDrawer == null)
            {
                radarBScopeDrawer = new RadarBScopeMFD(new Vector2Int(1024, 1024), Color.black);
            }
            if (radarPPIDrawer == null)
            {
                radarPPIDrawer = new RadarPPIMFD(new Vector2Int(1024, 1024), Color.black);   // 原来写成了 radarBScopeDrawer（复制粘贴 bug：PPI 为 null 时会 NRE，且新 drawer 直接泄漏） // 臭肥鱼你怎么什么都说
            }
            if (gimbalDrawer == null)
            {
                gimbalDrawer = new GimbalMFD(new Vector2Int(1024, 1024), Color.black);
            }

            radarBScopeDrawer.Init(parentAircraft);
            radarPPIDrawer.Init(parentAircraft);
            gimbalDrawer.Init(parentAircraft);
            gimbalDrawer.SetFont(stdFont);
        }

        /// <summary>
        /// Init MFD for Renderer
        /// </summary>
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
                case (MFDType.Gimbal):
                    return gimbalDrawer;
                default :
                    return null;
            }
        }

        private static RenderTexture emptyMFDTexture;

        /// <summary>
        /// Get a empty MFD texture when there's nothing to display.
        /// </summary>
        private static RenderTexture GetEmptyMFDTexture()
        {
            if (emptyMFDTexture == null)
            {
                // TODO: 空 MFD 槽位共用一个 1x1 RT。以前每次 Init 都 new 一个且从不释放 → Persistent 泄漏。
                emptyMFDTexture = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32)
                {
                    enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                emptyMFDTexture.Create();
            }
            return emptyMFDTexture;
        }

        private RenderTexture GetMFDTexture(ScreenProperty display)
        {
            MFDType type = display.type;
            switch (type)
            {
                case (MFDType.None):
                    return GetEmptyMFDTexture();
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
                gimbalDrawer.UpdateCanvas();
            }
        }

        private void OnApplicationQuit()
        {
            // Dispose all resources when quit.
            if (parentAircraft != null && parentAircraft.isControlling)
            {
                radarBScopeDrawer?.Dispose();
                radarPPIDrawer?.Dispose();
                gimbalDrawer?.Dispose();
            }
            if (emptyMFDTexture != null)
            {
                emptyMFDTexture.Release();
                Destroy(emptyMFDTexture);
                emptyMFDTexture = null;
            }
        }
    }
}