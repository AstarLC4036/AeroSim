using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.UI;
using AeroSim.Utils;
using System;
using TMPro;
using UnityEngine;

namespace AeroSim.Cockpit.MFD.CN.J10C
{
    [Serializable]
    public class RadarBScopeMasterPage : IMFD_Page
    {
        private MFDDataContext data;
        private MFDGraphicHelper drawer;
        private Aircraft parentAircraft;
        private RadarModule radar;
        private DatalinkModule datalink;
        public Rect zone;
        public float maxDrawDistance;
        public Vector2Int cursorDisplayPosition;
        public Vector2Int size;
        public bool isDatalinkAvaliable => datalink != null;

        public Rect Zone => zone;
        public MFD_PageType Type => MFD_PageType.RadarBScope;

        public void Init(MFDDataContext ctx, MFDGraphicHelper drawer)
        {
            data = ctx;
            this.drawer = drawer;

            parentAircraft = ctx.aircraft;
            radar = ctx.aircraft.radar;

            if (parentAircraft.datalink != null)
            {
                datalink = parentAircraft.datalink;
            }
        }

        public void OnButtonClick(int buttonIndex)
        {

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

        public void DrawPage()
        {
            UpdateData();
            DrawRadar();
            DrawSeparator();
        }

        public void DrawRadar()
        {
            Vector2Int areaSize = new Vector2Int(size.x * 3 / 4, size.x * 3 / 4);
            Vector2Int areaOffset = new Vector2Int(size.x * 1 / 4 / 2, size.y * 9 / 20);

            // Border
            drawer.DrawRectOutline(areaOffset.x, areaOffset.y, areaSize.x, areaSize.y, 5, Color.white);

            // Radar radius
            drawer.DrawRectFill(areaOffset.x, areaOffset.y - 9, areaSize.x, 12, new Color32(255, 100, 240, 130));
            drawer.DrawRectFill(areaOffset.x, areaOffset.y - 3, areaSize.x, 12, new Color32(50, 255, 50, 130));

            drawer.SetClipRect(new Rect(areaOffset, areaSize));

            // Scaler
            for (int i = 1; i < 4; i++)
            {
                drawer.DrawRectFill(areaOffset.x + areaSize.x / 4 * i - 2, areaOffset.y + 3, 5, 30, Color.white);
                drawer.DrawRectFill(areaOffset.x + areaSize.x / 4 * i - 2, areaOffset.y + areaSize.y - 30, 5, 30, Color.white);
                drawer.DrawRectFill(areaOffset.x + 2, areaOffset.y + areaSize.y / 4 * i - 3, 30, 5, Color.white);
                drawer.DrawRectFill(areaOffset.x + areaSize.x - 30, areaOffset.y + areaSize.y / 4 * i - 2, 30, 5, Color.white);

                if (i < 3)
                {
                    for (int j = 1; j < 3; j++)
                    {
                        drawer.DrawRectFill(areaOffset.x + 1, areaOffset.y + areaSize.y / 12 * (3 * i + j), 25, 5, Color.white); // i / 4 + j / 3 * (1 / 4) -> i/4 + j/12 -> 3i/12 + j/12 -> (3i + j) / 12
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
            if (posVectorY > areaOffset.y && posVectorY < areaOffset.y + areaSize.y)
            {
                drawer.DrawCircle(areaOffset.x + areaSize.x / 2, (int)posVectorY, 12, 4, Color.white);
                drawer.DrawRectFillCenter(areaOffset.x + areaSize.x / 2, (int)posVectorY + 20, 2, 10, Color.white);
                drawer.DrawRectFillCenter(areaOffset.x + areaSize.x / 2 + 20, (int)posVectorY, 10, 2, Color.white);
                drawer.DrawRectFillCenter(areaOffset.x + areaSize.x / 2 - 20, (int)posVectorY, 10, 2, Color.white);
            }

            DrawCursor(areaOffset.x + cursorDisplayPosition.x * areaSize.x / RadarHUDDrawer.Instance.size.x, areaOffset.y + cursorDisplayPosition.y * areaSize.y / RadarHUDDrawer.Instance.size.y);

            for (int i = 0; i < radar.ScannedAircrafts.Count; i++)
            {
                Aircraft aircraft = radar.ScannedAircrafts[i];
                var (posX, posY) = TransformWorldToRadar(aircraft.transform.position, areaSize);
                Vector3 veloDir = data.aircraft.transform.InverseTransformDirection(aircraft.Velocity);
                float dirAngle = Mathf.Atan2(veloDir.x, veloDir.z);

                //drawer.DrawCircle(areaOffset.x + posX, areaOffset.y + posY, 28, 6, Color.white);
                //drawer.DrawText((i + 1).ToString(), areaOffset.x + posX, areaOffset.y + posY - 18, Color.white, size: 36, anchor: MFDTextAnchor.Center);
                DrawScanTarget(new Vector2(areaOffset.x + posX, areaOffset.y + posY), dirAngle * Mathf.Rad2Deg + 90);
            }

            if (isDatalinkAvaliable)
            {
                foreach (var mslAndTarget in parentAircraft.datalink.mslTrackInfo)
                {
                    Missile missile = mslAndTarget.Item1;
                    var (posX, posY) = TransformWorldToRadar(missile.transform.position, areaSize);
                    var (posTX, posTY) = TransformWorldToRadar(missile.target.position, areaSize);

                    drawer.DrawRectFillCenter(areaOffset.x + posX, areaOffset.y + posY, 4, 4, Color.white);
                    if (missile.IsIgnited)
                        drawer.DrawLine(areaOffset.x + posX, areaOffset.y + posY, areaOffset.x + posTX, areaOffset.y + posTY, Color.white);
                    else
                        drawer.DrawDashedLine(areaOffset.x + posX, areaOffset.y + posY, areaOffset.x + posTX, areaOffset.y + posTY, Color.white, 8, 8);
                }
            }

            drawer.ClearClip();
        }

        private void DrawCursor(int x0, int y0)
        {
            drawer.DrawLine(x0 - 36, y0 - 36, x0 - 36, y0 + 36, Color.white);
            drawer.DrawLine(x0 + 36, y0 - 36, x0 + 36, y0 + 36, Color.white);
        }

        private void DrawScanTarget(Vector2 pos, float angle)
        {
            const float size = 50;
            angle = 180 - angle;
            float angle1 = angle + 180 + 30;
            float angle2 = angle + 180 - 30;
            Vector2 dir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle), Mathf.Sin(Mathf.Deg2Rad * angle));
            Vector2 delta = dir * (size / 1.7321f); // 1.7321 -> about sqrt3
            Vector2 p1 = pos + new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle1), Mathf.Sin(Mathf.Deg2Rad * angle1)) * size + delta;
            Vector2 p2 = pos + new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle2), Mathf.Sin(Mathf.Deg2Rad * angle2)) * size + delta;
            Vector2 p3 = pos + dir * size + delta;


            drawer.DrawLine(pos + delta, p1, Color.yellow, width: 5);
            drawer.DrawLine(pos + delta, p2, Color.yellow, width: 5);
            drawer.DrawLine(p1, p2, Color.yellow, width: 5);
            drawer.DrawLine(pos + delta, p3, Color.yellow, width: 5);
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

        void DrawSeparator()
        {
            drawer.DrawRectFillCenter(512, size.y - 1024, 512, 2, Color.white);
        }
    }
}
