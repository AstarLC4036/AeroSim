using AeroSim.AeroPhysics;
using AeroSim.UI;
using AeroSim.Utils;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace AeroSim.Cockpit.MFD.CN.J10C
{
    [Serializable]
    public class GimbalMasterPage : IMFD_Page
    {
        private MFDDataContext data;
        private MFDGraphicHelper drawer;
        private Aircraft parentAircraft;
        public Rect zone;
        public Color bgColor;
        public Rect gimbalRect;
        public float gimbalContentW;
        public float gimbalStencilRadius;
        public Color skyColor;
        public Color groundColor;

        public Rect Zone => zone;
        public MFD_PageType Type => MFD_PageType.Gimbal;

        public void Init(MFDDataContext ctx, MFDGraphicHelper drawer)
        {
            data = ctx;
            this.drawer = drawer;
            this.parentAircraft = ctx.aircraft;
            bgColor = ctx.mfdDrawer.bgColor;
        }

        public void DrawPage()
        {
            DrawGimbal();
        }

        public void OnButtonClick(int buttonIndex)
        {

        }

        void DrawGimbal()
        {
            float rollAngle = parentAircraft.transform.eulerAngles.z;
            drawer.SetClipRect(gimbalRect.x - gimbalRect.width / 2, gimbalRect.y - gimbalRect.height / 2, gimbalRect.width, gimbalRect.height);
            drawer.DrawHalfPlane(gimbalRect.x, gimbalRect.y, -rollAngle + 180, gimbalContentW, skyColor);
            drawer.DrawHalfPlane(gimbalRect.x, gimbalRect.y, -rollAngle, gimbalContentW, groundColor);
            drawer.FillOutsideDisc(gimbalRect.x, gimbalRect.y, gimbalStencilRadius, bgColor);
            drawer.ClearClip();
        }
    }
}