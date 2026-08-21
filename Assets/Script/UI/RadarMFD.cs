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
    public class RadarMFD : MFDDrawer
    {
        private Aircraft parentAircraft;
        private RadarModule radar;
        private DatalinkModule datalink;
        public Color32 scanRangeColor = new Color(0, 185, 0, 180);
        public Color32 scanRangeOutlineColor = new Color(0, 185, 0, 200);
        public float maxDrawDistance;
        public int gridAngleX = 30;
        public int gridAngleY = 30;
        public int vecLineLength = 30;
        public Vector2Int cursorDisplayPosition;
        private bool isDatalinkAvaliable => datalink != null;
        public void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;
            radar = aircraft.radar;

            InitCanavs();

            if (parentAircraft.datalink != null)
            {
                datalink = parentAircraft.datalink;
            }
        }

        private void UpdateData()
        {
            if (parentAircraft == Aircraft.main)
            {
                Vector2Int cursorPos = RadarHUDDrawer.Instance.cursorDisplayPosition;
                cursorDisplayPosition = cursorPos;

                maxDrawDistance = RadarHUDDrawer.Instance.maxDrawDistance;
            }
        }

        public override void ProcessCanvas()
        {
            UpdateData();

            DrawRadar();

            //DrawBorder(3, Color.green);
            //DrawBorder(2, bgColor);
        }

        // For J-10C with AESA Radar
        private void DrawRadar()
        {
            Vector2Int areaSize = new Vector2Int(size.x * 3 / 4, size.x * 3 / 4);
            Vector2Int areaOffset = new Vector2Int(size.x * 1 / 4 / 2, size.y * 9 / 20);

            // Border
            drawer.DrawRect(areaOffset.x, areaOffset.y, areaSize.x, areaSize.y, 3, Color.white);

            // Radar radius
            drawer.DrawRect(areaOffset.x, areaOffset.y - 9, areaSize.x, 12, new Color32(255, 100, 240, 255));
            drawer.DrawRect(areaOffset.x, areaOffset.y - 3, areaSize.x, 12, new Color32(50, 255, 50, 255));

            // Scaler
            for (int i = 1; i < 4; i++)
            {
                drawer.DrawRect(areaOffset.x + areaSize.x / 4 * i - 1, areaOffset.y + 1, 3, 15, Color.white);
                drawer.DrawRect(areaOffset.x + areaSize.x / 4 * i - 1, areaOffset.y + areaSize.y - 15, 3, 15, Color.white);
                drawer.DrawRect(areaOffset.x + 1, areaOffset.y + areaSize.y / 4 * i - 1, 15, 3, Color.white);
                drawer.DrawRect(areaOffset.x + areaSize.x - 15, areaOffset.y + areaSize.y / 4 * i - 1, 15, 3, Color.white);

                if(i < 3)
                {
                    for(int j = 1; j < 3; j++)
                    {
                        drawer.DrawRect(areaOffset.x + 1, areaOffset.y + areaSize.y / 12 * (3 * i + j), 12, 3, Color.white); // i / 4 + j / 3 * (1 / 4) -> i/4 + j/12 -> 3i/12 + j/12 -> (3i + j) / 12
                    }
                }
            }

            // Draw Grid
            for (int i = 1; i < 4; i += 2)
            {
                drawer.DrawLine(areaOffset.x + areaSize.x / 4 * i, areaOffset.y, areaOffset.x + areaSize.x / 4 * i, areaOffset.y + areaSize.y, Color.white);
                drawer.DrawLine(areaOffset.x, areaOffset.y + areaSize.y / 4 * i, areaOffset.x + areaSize.x, areaOffset.y + areaSize.y / 4 * i, Color.white);
            }

            drawer.DrawLine(areaOffset.x + areaSize.x / 2, areaOffset.y, areaOffset.x + areaSize.x / 2, areaOffset.y + areaSize.y * 3 / 10, Color.white);
            drawer.DrawLine(areaOffset.x + areaSize.x / 2, areaOffset.y + areaSize.y, areaOffset.x + areaSize.x / 2, areaOffset.y + areaSize.y * 7 / 10, Color.white);
            drawer.DrawLine(areaOffset.x, areaOffset.y + areaSize.y / 2, areaOffset.x + areaSize.x * 3 / 14, areaOffset.y + areaSize.y / 2, Color.white);
            drawer.DrawLine(areaOffset.x + areaSize.x, areaOffset.y + areaSize.y / 2, areaOffset.x + areaSize.x * 11 / 14, areaOffset.y + areaSize.y / 2, Color.white);

            // Draw Gimbal
            float roll = -parentAircraft.transform.eulerAngles.z * Mathf.Deg2Rad;
            drawer.DrawLine(areaOffset.x + areaSize.x / 2 + (int)(Mathf.Cos(roll) * 15), 
                            areaOffset.y + areaSize.y / 2 + (int)(Mathf.Sin(roll) * 15),
                            areaOffset.x + areaSize.x / 2 + (int)(Mathf.Cos(roll) * (areaSize.x / 4 - 5)),
                            areaOffset.y + areaSize.y / 2 + (int)(Mathf.Sin(roll) * (areaSize.x / 4 - 5)),
                            Color.white);
            drawer.DrawLine(areaOffset.x + areaSize.x / 2 - (int)(Mathf.Cos(roll) * 15), 
                            areaOffset.y + areaSize.y / 2 - (int)(Mathf.Sin(roll) * 15),
                            areaOffset.x + areaSize.x / 2 - (int)(Mathf.Cos(roll) * (areaSize.x / 4 - 5)),
                            areaOffset.y + areaSize.y / 2 - (int)(Mathf.Sin(roll) * (areaSize.x / 4 - 5)),
                            Color.white);
            drawer.DrawLine(areaOffset.x + areaSize.x / 2 + (int)(Mathf.Cos(roll) * (areaSize.x / 4 - 5)),
                            areaOffset.y + areaSize.y / 2 + (int)(Mathf.Sin(roll) * (areaSize.x / 4 - 5)),
                            areaOffset.x + areaSize.x / 2 + (int)(Mathf.Cos(roll) * (areaSize.x / 4 - 5) + Mathf.Sin(roll) * 6),
                            areaOffset.y + areaSize.y / 2 + (int)(Mathf.Sin(roll) * (areaSize.x / 4 - 5) - Mathf.Cos(roll) * 6),
                            Color.white);
            drawer.DrawLine(areaOffset.x + areaSize.x / 2 + (int)(-Mathf.Cos(roll) * (areaSize.x / 4 - 5)),
                            areaOffset.y + areaSize.y / 2 + (int)(-Mathf.Sin(roll) * (areaSize.x / 4 - 5)),
                            areaOffset.x + areaSize.x / 2 + (int)(-Mathf.Cos(roll) * (areaSize.x / 4 - 5) + Mathf.Sin(roll) * 6),
                            areaOffset.y + areaSize.y / 2 + (int)(-Mathf.Sin(roll) * (areaSize.x / 4 - 5) - Mathf.Cos(roll) * 6),
                            Color.white);

            // Draw Velocity Vector
            Vector3 velocityDir = parentAircraft.Velocity.normalized;
            float pitchAngle = Mathf.Asin(velocityDir.y) * Mathf.Rad2Deg;
            float posVectorY = areaOffset.y + areaSize.y / 2 * (1 + (pitchAngle / radar.currentScanAngleY));
            if(posVectorY > areaOffset.y && posVectorY < areaOffset.y + areaSize.y)
            {
                drawer.DrawCircle(areaOffset.x + areaSize.x / 2, (int)posVectorY, 6, Color.white, 3);
                drawer.DrawRect(areaOffset.x + areaSize.x / 2 - 1, (int)posVectorY + 6, 3, 6, Color.white);
                drawer.DrawRect(areaOffset.x + areaSize.x / 2 + 6, (int)posVectorY, 6, 3, Color.white);
                drawer.DrawRect(areaOffset.x + areaSize.x / 2 - 12, (int)posVectorY, 6, 3, Color.white);
            }

            DrawCursor(areaOffset.x + cursorDisplayPosition.x * areaSize.x / RadarHUDDrawer.Instance.resolution, areaOffset.y + cursorDisplayPosition.y * areaSize.y / RadarHUDDrawer.Instance.resolution);

            foreach(Aircraft aircraft in radar.ScannedAircrafts)
            {
                var (posX, posY) = TransformWorldToRadar(aircraft.transform.position, areaSize);

                drawer.DrawCircle(areaOffset.x + posX, areaOffset.y + posY, 10, Color.white, 3);
            }

            if(isDatalinkAvaliable)
            {
                foreach(Missile missile in parentAircraft.datalink.missiles)
                {
                    var (posX, posY) = TransformWorldToRadar(missile.transform.position, areaSize);
                    var (posTX, posTY) = TransformWorldToRadar(missile.target.position, areaSize);

                    drawer.DrawRectCenter(areaOffset.x + posX, areaOffset.y + posY, 4, 4, Color.white);
                    if(missile.IsIgnited)
                        drawer.DrawLine(areaOffset.x + posX, areaOffset.y + posY, areaOffset.x + posTX, areaOffset.y + posTY, Color.white);
                    else
                        drawer.DrawDashedLine(areaOffset.x + posX, areaOffset.y + posY, areaOffset.x + posTX, areaOffset.y + posTY, Color.white, 8, 8);
                }
            }

            //// Draw grid for different mode
            //if (radar.radarMode == RadarModule.RadarMode.HMD)
            //{
            //    Vector3 radarFwd = radar.transform.forward;
            //    Vector3 hmdDir = radar.hmdPointer.forward;
            //    int posX = size.x / 2 + (int)((Mathf.Atan2(hmdDir.x, hmdDir.z) - Mathf.Atan2(radarFwd.x, radarFwd.z)) * Mathf.Rad2Deg / radar.maxScanAngleX * (size.x / 2));
            //    int width = (int)(radar.currentScanAngleX / radar.maxScanAngleX * (size.x / 2));
            //    drawer.DrawLine(posX - width, 0, posX - width, size.y, scanRangeOutlineColor);
            //    drawer.DrawLine(posX + width, 0, posX + width, size.y, scanRangeOutlineColor);
            //}
            //else
            //{
            //    // Draw grid
            //    int girdGapHeight = (int)(gridAngleY / radar.displayAngleY * (size.y / 2));
            //    int girdGapWidth = (int)(gridAngleX / radar.displayAngleX * (size.x / 2));

            //    for (int i = 0; i <= (int)(radar.currentScanAngleX * 2 / gridAngleX); i++)
            //    {
            //        drawer.DrawLine(i * girdGapWidth, 0, i * girdGapWidth, size.y, new Color32(0, 255, 0, 255));
            //    }
            //    for (int i = 0; i <= (int)(radar.currentScanAngleY * 2 / gridAngleY); i++)
            //    {
            //        drawer.DrawLine(0, i * girdGapHeight, size.x, i * girdGapHeight, new Color32(0, 255, 0, 255));

            //        drawer.DrawLine((int)(radar.currentScanAngleX / radar.displayAngleX * size.x) - 1, 0, (int)(radar.currentScanAngleX / radar.displayAngleX * size.x) - 1, size.y, new Color32(0, 255, 0, 255));
            //        drawer.DrawLine(0, (int)(radar.currentScanAngleY / radar.displayAngleY * size.y) - 1, size.x, (int)(radar.currentScanAngleY / radar.displayAngleY * size.y) - 1, new Color32(0, 255, 0, 255));
            //    }
            //}

            //// Draw vertical angle sign
            //drawer.DrawRect(5, size.y * 3 / 4, 5, 2, new Color32(0, 255, 0, 196));
            //drawer.DrawRect(5, size.y / 4, 5, 2, new Color32(0, 255, 0, 196));

            //// Different target draw mode
            //if (!radar.IsTracking) // TWS, SRC etc mode
            //{
            //    // Draw cursor
            //    DrawCursor(cursorDisplayPosition);

            //    // Draw all scanned aircrafts
            //    foreach (Aircraft aircraft in radar.ScannedAircrafts)
            //    {
            //        //int posX, posY;
            //        var (posX, posY) = TransformWorldToRadar(aircraft.transform.position);
            //        Vector2Int acPos = new Vector2Int(posX, posY);

            //        drawer.DrawRect(posX, posY, 7, 2, new Color32(0, 255, 0, 255));
            //    }

            //    // Datalink drawer
            //    if (isDatalinkAvaliable)
            //    {
            //        // Missiles
            //        foreach (Missile msl in datalink.missiles)
            //        {
            //            var (posX, posY) = TransformWorldToRadar(msl.transform.position);
            //            var (posTX, posTY) = TransformWorldToRadar(msl.target.transform.position);

            //            drawer.DrawRect(posX, posY, 3, 3, Color.green);
            //            drawer.DrawLine(posX, posY, posTX, posTY, Color.green);
            //        }
            //    }
            //}
            //else // STT/TRK mode
            //{
            //    var (posX, posY) = TransformWorldToRadar(radar.lockedAircraft.transform.position);

            //    Vector3 radarPos = parentAircraft.transform.InverseTransformPoint(radar.lockedAircraft.transform.position);
            //    Vector3 radarVeloPos = parentAircraft.transform.InverseTransformPoint(radar.lockedAircraft.transform.position + radar.lockedAircraft.Velocity * 100);
            //    Vector3 localVelo = (radarVeloPos - radarPos).normalized;

            //    drawer.DrawCircle(posX, posY, 6, new Color32(0, 255, 0, 255), 2);
            //    drawer.DrawLine(
            //        posX,
            //        posY,
            //        posX + (int)(localVelo.x * vecLineLength),
            //        posY + (int)(localVelo.z * vecLineLength),
            //        new Color32(0, 255, 0, 255)
            //        );
            //    drawer.DrawDashedLine(posX, posY, posX, 0, new Color32(0, 255, 0, 96), 3, 3);
            //}
        }

        private void DrawCursor(int x0, int y0)
        {
            //drawer.DrawRect(x0 - 14, y0, 1, 7, new Color32(0, 255, 0, 255));
            //drawer.DrawRect(x0 + 14, y0, 1, 7, new Color32(0, 255, 0, 255));
            drawer.DrawLine(x0 - 14, y0 - 14, x0 - 14, y0 + 14, Color.white);
            drawer.DrawLine(x0 + 14, y0 - 14, x0 + 14, y0 + 14, Color.white);
        }

        private (int, int) TransformWorldToRadar(Vector3 worldPos, Vector2Int size = new Vector2Int())
        {
            Vector3 localPos = parentAircraft.transform.InverseTransformPoint(worldPos);
            float dst = Vector3.Distance(parentAircraft.transform.position, worldPos);
            float angle = Mathf.Atan2(localPos.x, localPos.z) * Mathf.Rad2Deg;
            int posX = (int)(size.x / 2 + Mathf.Clamp(angle / radar.displayAngleX, -1, 1) * size.x * 0.5f);
            int posY = (int)(Mathf.Clamp01(dst / maxDrawDistance / 1000) * size.y);

            return (posX, posY);
        }
    }
}
