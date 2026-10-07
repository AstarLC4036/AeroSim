using AeroSim.Cockpit.MFD;
using AeroSim.UI;
using AeroSim.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Cockpit.MFD.CN.J10C
{
    [Serializable]
    public class PowerplantSlavePage : IMFD_Page
    {
        private MFDDataContext data;
        private MFDGraphicHelper drawer;

        public Color bgColor;
        public Vector2Int size;
        public Rect rect;
        public float rpmWidth;
        public float rpmIndicatorSize;
        public float rpmPaddingX;
        public float rpmGapWidth;
        public float fuelMarginWidth;
        public float fuelGapWidth;
        public float fuelDisplayWidth;

        public Rect Zone => rect;
        public MFD_PageType Type => MFD_PageType.Powerplant;

        public void Init(MFDDataContext ctx, MFDGraphicHelper drawer)
        {
            data = ctx;
            this.drawer = drawer;
            bgColor = ctx.mfdDrawer.bgColor;
            size = ctx.mfdDrawer.size;
        }

        public void OnButtonClick(int buttonIndex)
        {

        }

        public void DrawPage()
        {
            DrawPowerplant();
        }

        void DrawPowerplant()
        {
            //float N1 = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.EngineRPM_N1);
            float N2 = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.EngineRPM_N2);
            float T4 = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.EngineT4);
            DrawRPMIndicator(rect.x - rpmPaddingX, rect.y, N2); // -1/2 + 1/4
            DrawRPMIndicator(rect.x - rpmPaddingX + rpmGapWidth, rect.y, Mathf.Min(T4, 1500) / 1500); // -1/2 + 2/4
            drawer.DrawRectFill(
                rect.x - rpmPaddingX + rpmGapWidth + fuelMarginWidth,
                rect.y - rpmIndicatorSize / 2, fuelDisplayWidth,
                rpmIndicatorSize, Color.green);
            drawer.DrawRectFill(
                rect.x - rpmPaddingX + rpmGapWidth + fuelMarginWidth + fuelGapWidth,
                rect.y - rpmIndicatorSize / 2, fuelDisplayWidth,
                rpmIndicatorSize, Color.green);
            DrawRPMTextLabel(rect.x - rpmPaddingX, rect.y + rpmIndicatorSize / 2 + 20, "N2", N2);
            DrawRPMTextLabel(rect.x - rpmPaddingX + rpmGapWidth, rect.y + rpmIndicatorSize / 2 + 20, "T4", T4 / 100);
        }

        void DrawRPMIndicator(float x0, float y0, float param)
        {
            float angle = param * 0.91f * -180;
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
            drawer.DrawText($"{(int)(rpm * 1000) / 10}", x0 + 15, y0 + 10, Color.green, anchor: MFDTextAnchor.Left, size: 48);
        }
    }
}
