using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.UI;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace AeroSim.Cockpit
{
    [Serializable]
    public class DynamicHUDDrawer : MFDDrawer
    {
        public Aircraft parentAircraft;
        public HUDHolder parentHolder;
        [Header("Layout")]
        public float ladderMin = 200;
        public float ladderMax = 800;
        public float ladderPadding = 300;
        [Header("Speed Ladder")]
        public float spdPerUnit;
        public float spdLadderScalerHeight;
        public int spdLadderScalerSubunits;
        [Header("Altitude Ladder")]
        public float altPerUnit;
        public float altLadderScalerHeight;
        public int altLadderScalerSubunits;
        [Header("Gimbal")]
        public Vector2 gimbalCenter;
        public float gimbalDeltaAngle;
        public float gimbalRadius;
        public float gimbalScalerLength;
        public float gimbalIndicatorSize;
        public int gimbalHalfScalerCount;

        public DynamicHUDDrawer(Vector2Int size, Color32 bgColor) : base(size, bgColor)
        {

        }

        public void SetFont(TMP_FontAsset font)
        {
            drawer.TextFont = font;
        }

        public void SetAircraft(Aircraft aircraft)
        {
            parentAircraft = aircraft;
        }

        //HUD drawer for J-10, 1024 * 1024
        public override void ProcessCanvas()
        {
            // Pointer Cross
            drawer.DrawRectFillCenter(size.x / 2, size.y / 2, 2, 30, Color.green);
            drawer.DrawRectFillCenter(size.x / 2, size.y / 2, 30, 2, Color.green);

            // FPV
            Vector3 localVelocity = parentHolder.transform.InverseTransformDirection(parentAircraft.Velocity);

            if (localVelocity.z > 0.5f)
            {
                float pixelPerMeter = size.x / parentHolder.hudPhysicalSize;

                float offsetX = (localVelocity.x / localVelocity.z) * pixelPerMeter;
                float offsetY = (localVelocity.y / localVelocity.z) * pixelPerMeter;

                Vector2 center = new Vector2(size.x / 2, size.y / 2);

                float fpvX = center.x + offsetX;
                float fpvY = center.y + offsetY;

                drawer.DrawCircle(fpvX, fpvY, 20, 4, Color.green);

                drawer.DrawRectFillCenter(fpvX - 40, fpvY, 20f, 2f, Color.green);
                drawer.DrawRectFillCenter(fpvX + 40, fpvY, 20f, 2f, Color.green);
                drawer.DrawRectFillCenter(fpvX, fpvY + 40, 2f, 20f, Color.green);
            }

            // Basic Infomation
            drawer.DrawText($"a { (int)(parentAircraft.AOA * 10) / 10f }", ladderPadding - 40, size.y - 120, Color.green, 36);
            drawer.DrawText($"G { (int)(parentAircraft.G * 10) / 10f }", ladderPadding - 40, size.y - 160, Color.green, 36);
            drawer.DrawText($"M { (int)(CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.MachSpeed) * 10) / 10f }", ladderPadding - 40, 320, Color.green, 36);
            drawer.DrawText(CockpitDataManager.GetParsedWeaponState(), ladderPadding - 20, 280, Color.green, 36);
            drawer.DrawText($"INTC", ladderPadding - 30, 240, Color.green, 36);
            if(parentAircraft.radar != null)
                drawer.DrawText(Utils.Utilities.RadarStatusString(parentAircraft.radar), size.x - ladderPadding + 40, 320, Color.green, 36, anchor: MFDTextAnchor.Right);

            // markers
            //drawer.DrawRectOutline(60, 170, 50, 650, 3, Color.green); // area sign, delete before exec

            // Speed Ladder
            DrawSPDLadder();
            // Altitude Ladder
            DrawALTLadder();

            DrawGimbal();
        }

        void DrawSPDLadder()
        {
            float speed = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.Airspeed) / 1000 * 3600;
            int maxHalfSpeedUnitCount = Mathf.CeilToInt(650 / spdLadderScalerHeight / 2) + 1;
            float nearestSpeed = Mathf.CeilToInt(speed / spdPerUnit) * spdPerUnit;
            float heightOffset = (nearestSpeed - speed) / spdPerUnit * spdLadderScalerHeight;

            //Debug.Log($"spd {speed}, maxHalf {maxHalfSpeedUnitCount}, nearestSpd {nearestSpeed}, height offset {heightOffset}");

            for (int i = 0; i < maxHalfSpeedUnitCount; i++)
            {
                float spd = nearestSpeed + spdPerUnit * i;
                float yPos = size.y / 2 + heightOffset + spdLadderScalerHeight * i - 100;

                // sub scaler
                for (int j = 1; j < spdLadderScalerSubunits; j++)
                {
                    float ySubPos = yPos + (spdLadderScalerHeight / spdLadderScalerSubunits) * j;
                    if (ySubPos > ladderMax)
                        break;

                    drawer.DrawRectFill(ladderPadding, ySubPos, 20, 3, Color.green);
                }

                if (yPos > ladderMax)
                    break;

                // main scaler and text
                drawer.DrawRectFill(ladderPadding - 5, yPos, 25, 3, Color.green);
                drawer.DrawText($"{(int)spd}", ladderPadding - 15, yPos - 18, Color.green, 48, 0.1f, MFDTextAnchor.Right, -0.1f);
            }
            for (int i = 1; i < maxHalfSpeedUnitCount + 1; i++)
            {
                float spd = nearestSpeed - spdPerUnit * i;
                float yPos = size.y / 2 + heightOffset - spdLadderScalerHeight * i - 100;

                // speed should always bigger than zero
                if (spd < 0)
                    break;

                // sub scaler
                for (int j = 1; j < spdLadderScalerSubunits; j++)
                {
                    float ySubPos = yPos + (spdLadderScalerHeight / spdLadderScalerSubunits) * j;
                    if (ySubPos >= ladderMin)
                        drawer.DrawRectFill(ladderPadding, ySubPos, 20, 3, Color.green);
                    else
                        continue;
                }

                // main scaler and text
                if (yPos >= ladderMin)
                {
                    drawer.DrawRectFill(ladderPadding - 5, yPos, 25, 3, Color.green);
                    drawer.DrawText($"{(int)spd}", ladderPadding - 15, yPos - 18, Color.green, 48, 0.1f, MFDTextAnchor.Right, -0.1f);
                }
                else
                    continue;
            }

            drawer.DrawRectFill(ladderPadding - 80, size.y / 2 - 25, 100, 50, new Color(0, 0, 0, 0), (int)MFDBlend.Replace);
            drawer.DrawRectOutline(ladderPadding - 80, size.y / 2 - 25, 100, 50, 3, Color.green);
            drawer.DrawText($"{(int)speed}", ladderPadding - 75, size.y / 2 - 18, Color.green, 48, 0.1f, -0.1f);
        }

        void DrawALTLadder()
        {
            float altitude = CockpitDataManager.GetDataFloat(CockpitDataManager.CockpitDataType.Altitude);
            int maxHalfAltitudeUnitCount = Mathf.CeilToInt(650 / altLadderScalerHeight / 2) + 1;
            float nearestAltitude = Mathf.CeilToInt(altitude / altPerUnit) * altPerUnit;
            float heightOffset = (nearestAltitude - altitude) / altPerUnit * altLadderScalerHeight;

            for (int i = 0; i < maxHalfAltitudeUnitCount; i++)
            {
                float alt = nearestAltitude + altPerUnit * i;
                float yPos = size.y / 2 + heightOffset + altLadderScalerHeight * i;

                // sub scaler
                for (int j = 1; j < altLadderScalerSubunits; j++)
                {
                    float ySubPos = yPos + (altLadderScalerHeight / altLadderScalerSubunits) * j;
                    if (ySubPos > ladderMax)
                        break;

                    drawer.DrawRectFill(size.x - ladderPadding - 25, ySubPos, 20, 3, Color.green);
                }

                if (yPos > ladderMax)
                    break;

                // main scaler and text
                drawer.DrawRectFill(size.x - ladderPadding - 25, yPos, 25, 3, Color.green);
                drawer.DrawText($"{(int)(alt / 100) / 10f}", size.x - ladderPadding, yPos - 18, Color.green, 48, 0.1f, -0.1f);
            }
            for (int i = 1; i < maxHalfAltitudeUnitCount + 1; i++)
            {
                float alt = nearestAltitude - altPerUnit * i;
                float yPos = size.y / 2 + heightOffset - altLadderScalerHeight * i;

                if (alt < 0)
                    break;

                for (int j = 1; j < altLadderScalerSubunits; j++)
                {
                    float ySubPos = yPos + (altLadderScalerHeight / altLadderScalerSubunits) * j;
                    if (ySubPos >= ladderMin)
                        drawer.DrawRectFill(size.x - ladderPadding - 25, ySubPos, 20, 3, Color.green);
                    else
                        continue;
                }

                if (yPos >= ladderMin)
                {
                    drawer.DrawRectFill(size.x - ladderPadding - 25, yPos, 25, 3, Color.green);
                    drawer.DrawText($"{(int)(alt / 100) / 10f}", size.x - ladderPadding, yPos - 18, Color.green, 48, 0.1f, -0.1f);
                }
                else
                    continue;
            }

            drawer.DrawRectFill(size.x - ladderPadding - 25, size.y / 2 - 25, 100, 50, new Color(0, 0, 0, 0), (int)MFDBlend.Replace);
            drawer.DrawRectOutline(size.x - ladderPadding - 25, size.y / 2 - 25, 100, 50, 3, Color.green);
            drawer.DrawText($"{(int)altitude}", size.x - ladderPadding - 20, size.y / 2 - 18, Color.green, 48, 0.1f, -0.1f);
        }

        void DrawGimbal()
        {
            float rollAngle = parentAircraft.transform.eulerAngles.z;
            for(int i = 0; i < gimbalHalfScalerCount; i++)
            {
                float angle = (gimbalDeltaAngle * i - 90) * Mathf.Deg2Rad;

                Vector2 pointInner = new Vector2(Mathf.Cos(angle) * gimbalRadius, Mathf.Sin(angle) * gimbalRadius);
                Vector2 pointOuter = pointInner * ((gimbalScalerLength + gimbalRadius) / gimbalRadius);

                if(i != 0)
                {
                    Vector2 pointLInner = gimbalCenter + pointInner;
                    Vector2 pointLOuter = gimbalCenter + pointOuter;
                    Vector2 pointRInner = gimbalCenter + new Vector2(-pointInner.x, pointInner.y);
                    Vector2 pointROuter = gimbalCenter + new Vector2(-pointOuter.x, pointOuter.y);

                    drawer.DrawLine(pointLInner, pointLOuter, Color.green, 10);
                    drawer.DrawLine(pointRInner, pointROuter, Color.green, 10);
                }
                else
                {
                    Vector2 pointMInner = gimbalCenter + pointInner;
                    Vector2 pointMOuter = gimbalCenter + pointOuter * ((gimbalScalerLength * 1.2f + gimbalRadius) / gimbalRadius);

                    drawer.DrawLine(pointMInner, pointMOuter, Color.green, 10);
                }
            }
            Vector2 indicatorPos = new Vector2(Mathf.Cos((rollAngle - 90) * Mathf.Deg2Rad), Mathf.Sin((rollAngle - 90) * Mathf.Deg2Rad)) * (gimbalScalerLength + gimbalRadius) + gimbalCenter;
            DrawGimbalIndicator(indicatorPos, rollAngle - 90);
        }

        void DrawGimbalIndicator(Vector2 p0, float angle)
        {
            Vector2 d1 = new Vector2(
                Mathf.Cos((angle + 45) * Mathf.Deg2Rad), 
                Mathf.Sin((angle + 45) * Mathf.Deg2Rad)
            );
            Vector2 d2 = new Vector2(
                Mathf.Cos((angle - 45) * Mathf.Deg2Rad), 
                Mathf.Sin((angle - 45) * Mathf.Deg2Rad)
            );

            drawer.DrawLine(p0, p0 + d1 * gimbalIndicatorSize, Color.green, 10);
            drawer.DrawLine(p0, p0 + d2 * gimbalIndicatorSize, Color.green, 10);
            drawer.DrawLine(p0 + d1 * gimbalIndicatorSize, p0 + d2 * gimbalIndicatorSize, Color.green, 10);
        }
    }
}