using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.UI;
using AeroSim.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Cockpit.MFD.CN.J10C
{
    [Serializable]
    public class RadarPPIMMasterPage : IMFD_Page
    {
        private MFDDataContext data;
        private MFDGraphicHelper drawer;
        private Aircraft parentAircraft;
        private RadarModule radar;
        private DatalinkModule datalink;
        public Color32 scanScalerColor = new Color(0, 185, 0, 180);
        public Vector2Int size;
        public Rect zone;
        public Vector2 posDelta;
        public float maxDrawDistance;
        public int ppiRadius;
        public int scalerCount = 3;

        public Rect Zone => zone;
        public MFD_PageType Type => MFD_PageType.RadarPPI;

        public void Init(MFDDataContext ctx, MFDGraphicHelper drawer)
        {
            data = ctx;
            
            parentAircraft = ctx.aircraft;
            radar = ctx.aircraft.radar;
            this.drawer = drawer;

            if (parentAircraft.datalink != null)
            {
                datalink = parentAircraft.datalink;
            }
        }

        public void DrawPage()
        {
            DrawRadar();
        }

        public void OnButtonClick(int buttonIndex)
        {

        }

        void DrawRadar()
        {
            // draw border
            drawer.DrawCircle(size.x / 2 + posDelta.x, size.y / 2 + posDelta.y, ppiRadius, 5, Color.white);
            for(int i = 1; i < scalerCount; i++) // 'i = 0' will draw a circle with radius 0, which is not needed
            {
                int radius = (ppiRadius / scalerCount) * i;
                drawer.DrawCircle(size.x / 2 + posDelta.x, size.y / 2 + posDelta.y, radius, 5, scanScalerColor);
            }

            // draw plane
            drawer.DrawRectFillCenter(size.x / 2 + posDelta.x, size.y / 2 + 5 + posDelta.y, 3, 15, Color.white);
            drawer.DrawRectFillCenter(size.x / 2 + posDelta.x, size.y / 2 + 8 + posDelta.y, 14, 3, Color.white);
            drawer.DrawRectFillCenter(size.x / 2 + posDelta.x, size.y / 2 - 10 + posDelta.y, 8, 3, Color.white);

            foreach(Aircraft aircraft in radar.ScannedAircrafts)
            {
                Vector2Int pos = TransformPositionToPPI(aircraft.transform.position, size.x / 2, size.y / 2);
                drawer.DrawRectFillCenter(pos.x + posDelta.x, pos.y + posDelta.y, 6, 6, Color.white);
            }
        }

        Vector2Int TransformPositionToPPI(Vector3 position, int cx, int cy)
        {
            Vector3 relativePosition = position - radar.transform.position;
            float dst = relativePosition.magnitude;
            float worldRelativeAngle = Mathf.Atan2(relativePosition.x, relativePosition.z);
            Vector3 radarForward = radar.transform.forward;
            float radarForwardAngle = Mathf.Atan2(radarForward.x, radarForward.z);
            float localAngle = radarForwardAngle - worldRelativeAngle + Mathf.PI / 2;
            float screenDst = (dst / (maxDrawDistance * 1000)) * ppiRadius;

            Vector2Int result = new Vector2Int((int)(cx + Mathf.Cos(localAngle) * screenDst), (int)(cy + Mathf.Sin(localAngle) * screenDst));
            return result;
        }

        Vector2Int TransformPositionToPPI(Vector3 position, Vector2Int center)
        {
            return TransformPositionToPPI(position, center.x, center.y);
        }
    }
}
