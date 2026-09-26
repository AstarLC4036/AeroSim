using AeroSim.AeroPhysics;
using AeroSim.Cockpit;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace AeroSim.UI
{
    [Serializable]
    public class GimbalMFD : MFDDrawer
    {
        private Aircraft parentAircraft;
        [Header("Top - Gimbal")]
        public Rect gimbalRect;
        public float gimbalContentW;
        public float gimbalStencilRadius;
        public Color skyColor;
        public Color groundColor;
        [Header("Bottom - Powerrplant")]
        public Rect powerplantRect;
        public float rpmWidth;
        public float rpmIndicatorSize;
        public float rpmPaddingX;
        public float rpmGapWidth;
        public float fuelMarginWidth;
        public float fuelGapWidth;
        public float fuelDisplayWidth;

        public GimbalMFD(Vector2Int size, Color bgColor) : base(size,bgColor)
        {
            
        }

        public void Init(Aircraft parentAircraft)
        {
            this.parentAircraft = parentAircraft;
            InitCanvas();
            drawer.SetDesignSize(1024, 1536);
        }

        public void SetFont(TMP_FontAsset font)
        {
            drawer.TextFont = font;
        }

        public override void ProcessCanvas()
        {
            DrawGimbal();
            drawer.DrawRectFillCenter(512, gimbalRect.y - gimbalRect.height / 2 - 40, 512, 3, Color.green);
            DrawPowerplant();
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

        void DrawPowerplant()
        {
            float N1 = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.EngineRPM_N1);
            float N2 = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.EngineRPM_N2);
            DrawRPMIndicator(powerplantRect.x - rpmPaddingX, powerplantRect.y, N1); // -1/2 + 1/4
            DrawRPMIndicator(powerplantRect.x - rpmPaddingX + rpmGapWidth, powerplantRect.y, N2); // -1/2 + 2/4
            drawer.DrawRectFill(
                powerplantRect.x - rpmPaddingX + rpmGapWidth + fuelMarginWidth, 
                powerplantRect.y - rpmIndicatorSize / 2, fuelDisplayWidth,
                rpmIndicatorSize, Color.green);
            drawer.DrawRectFill(
                powerplantRect.x - rpmPaddingX + rpmGapWidth + fuelMarginWidth + fuelGapWidth, 
                powerplantRect.y - rpmIndicatorSize / 2, fuelDisplayWidth,
                rpmIndicatorSize, Color.green);
            DrawRPMTextLabel(powerplantRect.x - rpmPaddingX, powerplantRect.y + rpmIndicatorSize / 2 + 20, "N1", N1);
            DrawRPMTextLabel(powerplantRect.x - rpmPaddingX + rpmGapWidth, powerplantRect.y + rpmIndicatorSize / 2 + 20, "N2", N2);
        }

        void DrawRPMIndicator(float x0, float y0, float N)
        {

            float angle = N * 0.91f * -180;
            drawer.DrawArc(x0, y0 + rpmIndicatorSize / 2, rpmIndicatorSize, rpmWidth, -180, 0, Color.green);
            drawer.DrawArc(x0, y0 + rpmIndicatorSize / 2, rpmIndicatorSize, rpmWidth - 10, -179, -1, bgColor, (int)MFDBlend.Replace);
            drawer.DrawArc(x0, y0 + rpmIndicatorSize / 2, rpmIndicatorSize, rpmWidth - 10, 0, angle, Color.green);
            DrawRPMNeedle(x0 + Mathf.Cos(angle * Mathf.Deg2Rad) * (rpmIndicatorSize - rpmWidth), y0 + Mathf.Sin(angle * Mathf.Deg2Rad) * (rpmIndicatorSize - rpmWidth) + rpmIndicatorSize / 2, angle);
        }

        void DrawRPMNeedle(float x0, float y0, float angle)
        {
            const float r0NeedleLen = 60;
            const float r1NeedleLen = 70;
            angle += 180;
            angle *= Mathf.Deg2Rad;
            float angle0 = angle - 10 * Mathf.Deg2Rad;
            float angle1 = angle + 10 * Mathf.Deg2Rad;
            Vector2 center = new Vector2(x0, y0);
            Vector2 p0 = new Vector2(Mathf.Cos(angle0) * r0NeedleLen + x0, Mathf.Sin(angle0) * r0NeedleLen + y0);
            Vector2 p1 = new Vector2(Mathf.Cos(angle1) * r0NeedleLen + x0, Mathf.Sin(angle1) * r0NeedleLen + y0);
            Vector2 p2 = new Vector2(Mathf.Cos(angle) * r1NeedleLen + x0, Mathf.Sin(angle) * r1NeedleLen + y0);

            drawer.DrawTriangle(center, p0, p2, Color.green);
            drawer.DrawTriangle(center, p1, p2, Color.green);
        }

        void DrawRPMTextLabel(float x0, float y0, string label, float rpm)
        {
            drawer.DrawText(label, x0, y0 + 10, Color.green, anchor: MFDTextAnchor.Right, size: 48);
            drawer.DrawRectOutline(x0 + 5, y0, 120, 50, 3f, Color.green);
            drawer.DrawText($"{ (int)(rpm * 1000) / 10 }", x0 + 15, y0 + 10, Color.green, anchor: MFDTextAnchor.Left, size: 48);
        }
    }
}