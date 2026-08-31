using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.UI
{
    [Serializable]
    public class RadarPPIMFD : MFDDrawer
    {
        private Aircraft parentAircraft;
        private RadarModule radar;
        private DatalinkModule datalink;
        public Color32 scanScalerColor = new Color(0, 185, 0, 180);
        public float maxDrawDistance;
        public int ppiRadius;
        public int scalerCount = 3;

        public RadarPPIMFD(Vector2Int size, Color32 bgColor) : base(size, bgColor)
        {

        }

        public void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;
            radar = aircraft.radar;

            InitCanvas();

            if (parentAircraft.datalink != null)
            {
                datalink = parentAircraft.datalink;
            }
        }

        public override void ProcessCanvas()
        {
            DrawRadar();
        }

        void DrawRadar()
        {
            // draw border
            drawer.DrawCircle(size.x / 2, size.y / 2, ppiRadius, 5, Color.white);
            for(int i = 0; i < scalerCount; i++)
            {
                int radius = (ppiRadius / scalerCount) * i;
                drawer.DrawCircle(size.x / 2, size.y / 2, radius, 5, scanScalerColor);
            }

            // draw plane
            drawer.DrawRectFillCenter(size.x / 2, size.y / 2 + 5, 3, 15, Color.white);
            drawer.DrawRectFillCenter(size.x / 2, size.y / 2 + 8, 14, 3, Color.white);
            drawer.DrawRectFillCenter(size.x / 2, size.y / 2 - 10, 8, 3, Color.white);

            foreach(Aircraft aircraft in radar.ScannedAircrafts)
            {
                Vector2Int pos = TransformPositionToPPI(aircraft.transform.position, size.x / 2, size.y / 2);
                drawer.DrawRectFillCenter(pos.x, pos.y, 6, 6, Color.white);
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
