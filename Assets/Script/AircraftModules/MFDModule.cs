using AeroSim.AeroPhysics;
using AeroSim.Cockpit.MFD;
using AeroSim.Cockpit.MFD.CN.J10C;
using AeroSim.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AeroSim.AircraftModules
{
    public class MFDModule : MonoBehaviour
    {
        [Serializable]
        public class ScreenProperty
        {
            [Header("Base")]
            public Renderer renderer;
            public int targetMaterialIndex;
            public Vector2 tiling;
            public Vector2 offset;
            public Vector2Int size;
            public Color activeBgColor;
            [Header("Setup")]
            public MFD_PageType[] initialPages;
        }

        public class ScreenRuntime
        {
            public ScreenProperty property;
            public MFDDrawer drawer;
            public MaterialPropertyBlock mpb;
        }

        public MFDPageSetup setup;
        public List<ScreenProperty> mfdScreens = new List<ScreenProperty>();
        public List<ScreenRuntime> mfdRuntime = new List<ScreenRuntime>();

        //参数参考 稍后删除:
        [SerializeField, HideInInspector]
        public RadarBScopeMasterPage radarBScopeDrawer;
        [SerializeField, HideInInspector]
        public RadarPPIMMasterPage radarPPIDrawer;
        [SerializeField, HideInInspector]
        public GimbalMasterPage gimbalDrawer;
        [SerializeField, HideInInspector]
        public PowerplantSlavePage powerplantDrawer;

        public TMP_FontAsset stdFont;
        // TODO: Chinese font

        private Aircraft parentAircraft;

        public void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;

            if (aircraft.isControlling)
            {
                InitMFD();
            }
        }

        /// <summary>
        /// Init MFD
        /// </summary>
        private void InitMFD()
        {
            foreach (ScreenProperty mfd in mfdScreens)
            {
                // set up renderer
                MaterialPropertyBlock block = new MaterialPropertyBlock();

                MultiZoneDisplay display = new MultiZoneDisplay(mfd.size, mfd.activeBgColor);

                // build runtime and context
                ScreenRuntime runtime = new ScreenRuntime()
                {
                    mpb = block,
                    property = mfd,
                    drawer = display
                };
                mfdRuntime.Add(runtime);

                MFDDataContext dataCtx = new MFDDataContext() { aircraft = parentAircraft, module = this, property = mfd, runtime = runtime, mfdDrawer = display};
                display.Init(dataCtx, setup); // we must init drawer before read its member field
                display.drawer.TextFont = stdFont;
                dataCtx.drawer = display.drawer;

                mfd.renderer.GetPropertyBlock(block, mfd.targetMaterialIndex);
                block.SetVector("_BaseMap_ST", new Vector4(mfd.tiling.x, mfd.tiling.y, mfd.offset.x, mfd.offset.y));
                block.SetTexture("_BaseMap", display.canvasTexture);
                mfd.renderer.SetPropertyBlock(block, mfd.targetMaterialIndex);
            }
        }

        public void Update()
        {
            if (parentAircraft.isControlling)
            {
                foreach (ScreenRuntime mfd in mfdRuntime)
                {
                    mfd.drawer.UpdateCanvas();
                }
            }
        }

        private void OnApplicationQuit()
        {
            // Dispose all resources when quit.
            if (parentAircraft != null && parentAircraft.isControlling)
            {
                foreach (ScreenRuntime mfd in mfdRuntime)
                {
                    mfd.drawer.Dispose();
                }
            }
        }
    }
}