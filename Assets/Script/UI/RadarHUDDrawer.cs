using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.Utils;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AeroSim.UI
{
    public class RadarHUDDrawer : MFDDisplay
    {
        private static RadarHUDDrawer instance;
        public static RadarHUDDrawer Instance => instance;

        //[Header("Config - MFD")]
        //public List<RawImage> drawTargets = new List<RawImage>();
        //public RawImage radarImg;
        //public int resolution = 260;
        //private Texture2D radarTex;
        //private Color32[] pixels;
        //private RadarGraphicHelper32 drawer;

        private Aircraft parentAircraft;
        private Aircraft selectedAircraft;
        private RadarModule radar;
        private DatalinkModule datalink;

        [Header("Config - Radar drawer")]
        //public Color32 bgColor = new Color(0, 185, 0, 128);
        public Color32 scanRangeColor = new Color(0, 185, 0, 180);
        public Color32 scanRangeOutlineColor = new Color(0, 185, 0, 200);
        public float maxDrawDistance;
        public int gridAngleX = 30;
        public int gridAngleY = 30;
        public int vecLineLength = 30;

        [Header("Config - UI")]
        public TMP_Text modeLabel; 
        public TMP_Text searchRangeLabel;
        public TMP_Text minAngleXLabel;
        public TMP_Text maxAngleXLabel;
        public TMP_Text angleRangeXLabel;
        public TMP_Text minAngleYLabel;
        public TMP_Text maxAngleYLabel;
        public TMP_Text dstRangeLabel;

        [Header("Cursor")]
        public Vector2 cursorPosition;
        public Vector2Int cursorDisplayPosition;
        public float cursorSpeed = 196;
        public float cursorSelectDistance = 10;
        private bool isCursorMoving = false;

        private bool isInited = false;
        private bool isDatalinkAvaliable => datalink != null;

        public static void SetAircraft(Aircraft aircraft)
        {
            instance.Init(aircraft);
        }

        public static void UpdateStatusLabel()
        {
            Instance.OnRadarStatusChange();
        }

        public void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;
            radar = aircraft.radar;

            InitCanavs();

            isInited = true;

            OnRadarStatusChange();

            radar.onLock += OnRadarLock;

            if(parentAircraft.datalink != null)
            {
                datalink = parentAircraft.datalink;
            }
        }

        private void Awake()
        {
            instance = this;
            cursorPosition = new Vector2(resolution / 2, resolution / 2);
            cursorDisplayPosition = new Vector2Int((int)cursorPosition.x, (int)cursorPosition.y);
        }

        // Update is called once per frame
        protected override void Update()
        {
            if (isInited)
            {
                UpdateCanvas();
                UpdateInput();
            }
        }

        public void OnRadarStatusChange()
        {
            modeLabel.text = Utility.Utilities.RadarStatusString(radar);
            searchRangeLabel.text = $"{radar.currentScanAngleX * 2}°x{radar.currentScanAngleY * 2}°";
            dstRangeLabel.text = $"{maxDrawDistance} km";
            minAngleXLabel.text = $"-{radar.displayAngleX}°";
            maxAngleXLabel.text = $"{radar.displayAngleX}°";
            minAngleYLabel.text = $"-{radar.displayAngleY}°";
            maxAngleYLabel.text = $"{radar.displayAngleY}°";
            angleRangeXLabel.text = $"{radar.maxScanAngleX * 2}°";
        }

        private void UpdateInput()
        {
            if(parentAircraft.isControlling)
            {
                UpdateSelectTarget();
            }
        }

        private void UpdateSelectTarget()
        {
            //fixed control
            if(Input.GetKey(KeyCode.UpArrow) ||  Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                if(!isCursorMoving)
                {
                    isCursorMoving = true;
                }
            }
            else if(isCursorMoving)
            {
                isCursorMoving = false;
            }

            if(Input.GetKey(KeyCode.UpArrow))
            {
                cursorPosition += new Vector2(0, cursorSpeed * Time.deltaTime);
                UpdateCursorPos();
            }
            if(Input.GetKey(KeyCode.DownArrow))
            {
                cursorPosition += new Vector2(0, -cursorSpeed * Time.deltaTime);
                UpdateCursorPos();
            }
            if(Input.GetKey(KeyCode.LeftArrow))
            {
                cursorPosition += new Vector2(-cursorSpeed * Time.deltaTime, 0);
                UpdateCursorPos();
            }
            if(Input.GetKey(KeyCode.RightArrow))
            {
                cursorPosition += new Vector2(cursorSpeed * Time.deltaTime, 0);
                UpdateCursorPos();
            }

            cursorPosition = new Vector2(Mathf.Clamp(cursorPosition.x, 0, resolution), Mathf.Clamp(cursorPosition.y, 0, resolution));
        }

        private void UpdateCursorPos()
        {
            cursorDisplayPosition = new Vector2Int((int)cursorPosition.x, (int)cursorPosition.y);
        }

        public override void ProcessCanvas()
        {
            // Draw grid for different mode
            if(radar.radarMode == RadarModule.RadarMode.HMD)
            {
                Vector3 radarFwd = radar.transform.forward;
                Vector3 hmdDir = radar.hmdPointer.forward;
                int posX = resolution / 2 + (int)((Mathf.Atan2(hmdDir.x, hmdDir.z) - Mathf.Atan2(radarFwd.x, radarFwd.z)) * Mathf.Rad2Deg / radar.maxScanAngleX * (resolution / 2));
                int width = (int)(radar.currentScanAngleX / radar.maxScanAngleX * (resolution / 2));
                drawer.DrawLine(posX - width, 0, posX - width, resolution, scanRangeOutlineColor);
                drawer.DrawLine(posX + width, 0, posX + width, resolution, scanRangeOutlineColor);
            }
            else
            {
                // Draw grid
                int girdGapHeight = (int)(gridAngleY / radar.displayAngleY * (resolution / 2));
                int girdGapWidth = (int)(gridAngleX / radar.displayAngleX * (resolution / 2));

                for (int i = 0; i <= (int)(radar.currentScanAngleX * 2 / gridAngleX); i++)
                {
                    drawer.DrawLine(i * girdGapWidth, 0, i * girdGapWidth, resolution, new Color32(0, 255, 0, 255));
                }
                for (int i = 0; i <= (int)(radar.currentScanAngleY * 2 / gridAngleY); i++)
                {
                    drawer.DrawLine(0, i * girdGapHeight, resolution, i * girdGapHeight, new Color32(0, 255, 0, 255));

                    drawer.DrawLine((int)(radar.currentScanAngleX / radar.displayAngleX * resolution) - 1, 0, (int)(radar.currentScanAngleX / radar.displayAngleX * resolution) - 1, resolution, new Color32(0, 255, 0, 255));
                    drawer.DrawLine(0, (int)(radar.currentScanAngleY / radar.displayAngleY * resolution) - 1, resolution, (int)(radar.currentScanAngleY / radar.displayAngleY * resolution) - 1, new Color32(0, 255, 0, 255));
                }
            }

            // Draw vertical angle sign
            drawer.DrawRectCenter(5, resolution * 3 / 4, 5, 2, new Color32(0, 255, 0, 196));
            drawer.DrawRectCenter(5, resolution / 4, 5, 2, new Color32(0, 255, 0, 196));

            // Different target draw mode
            if (!radar.IsTracking) // TWS, SRC etc mode
            {
                if (radar.ScannedAircrafts.Count > 0) // Update cursor position and draw cursor
                {
                    //// If no target is selected, select the first one
                    //if (selectedAircraft == null && !radar.ScannedAircrafts.Exists(x => x == selectedAircraft))
                    //{
                    //    selectedAircraft = radar.ScannedAircrafts[0]; // Don't lock, just select the first one
                    //}

                    Aircraft closetAc = null;

                    // Draw all scanned aircrafts
                    foreach (Aircraft aircraft in radar.ScannedAircrafts)
                    {
                        //int posX, posY;
                        var (posX, posY) = TransformWorldToRadar(aircraft.transform.position);
                        Vector2Int acPos = new Vector2Int(posX, posY);

                        if (radar.radarMode == RadarModule.RadarMode.SRC)
                            drawer.DrawRectCenter(posX, posY, 7, 2, new Color32(0, 255, 0, 255));
                        else if (radar.radarMode == RadarModule.RadarMode.TWS)
                        {
                            Vector3 localVector = radar.transform.InverseTransformDirection(aircraft.Velocity);
                            Vector3 normVector = Vector3.Normalize(new Vector3(localVector.x, 0, localVector.z));
                            drawer.DrawCircle(posX, posY, 10, Color.green, 3);
                            drawer.DrawLine(posX + (int)(normVector.x * 10), posY + (int)(normVector.y * 10), posX + (int)(normVector.x * 30), posY + (int)(normVector.z * 30), Color.green);
                        }

                        if (aircraft == selectedAircraft)
                        {
                            cursorDisplayPosition = acPos;
                        }

                        if(!isCursorMoving)
                        {
                            if (aircraft == selectedAircraft && cursorPosition != acPos)
                            {
                                cursorPosition = acPos;
                            }
                        }
                        else
                        {
                            float minDst = cursorSelectDistance;
                            float dst = Vector2.Distance(cursorPosition, acPos);
                            if(dst <  minDst)
                            {
                                minDst = dst;
                                closetAc = aircraft;
                            }
                        }

                        //if (aircraft == radar.lockedAircraft)
                        //{
                        //    DrawCursor(posX, posY);
                        //}
                    }

                    if(isCursorMoving && selectedAircraft != closetAc)
                    {
                        SelectCursorTarget(closetAc);
                    }

                    if(selectedAircraft == null)
                    {
                        cursorDisplayPosition = new Vector2Int((int)cursorPosition.x, (int)cursorPosition.y);
                    }
                }

                // Draw cursor
                DrawCursor(cursorDisplayPosition);
            }
            else // STT/TRK mode
            {
                var (posX, posY) = TransformWorldToRadar(radar.lockedAircraft.transform.position);

                Vector3 radarPos = parentAircraft.transform.InverseTransformPoint(radar.lockedAircraft.transform.position);
                Vector3 radarVeloPos = parentAircraft.transform.InverseTransformPoint(radar.lockedAircraft.transform.position + radar.lockedAircraft.Velocity * 100);
                Vector3 localVelo = (radarVeloPos - radarPos).normalized;

                drawer.DrawCircle(posX, posY, 6, new Color32(0, 255, 0, 255), 2);
                drawer.DrawLine(
                    posX,
                    posY,
                    posX + (int)(localVelo.x * vecLineLength),
                    posY + (int)(localVelo.z * vecLineLength),
                    new Color32(0, 255, 0, 255)
                    );
                drawer.DrawDashedLine(posX, posY, posX, 0, new Color32(0, 255, 0, 96), 3, 3);
            }

            // Datalink drawer
            if (isDatalinkAvaliable)
            {
                // Missiles
                foreach (Missile msl in datalink.missiles)
                {
                    var (posX, posY) = TransformWorldToRadar(msl.transform.position);
                    var (posTX, posTY) = TransformWorldToRadar(msl.target.position);

                    drawer.DrawRectCenter(posX, posY, 4, 4, Color.green);
                    if(msl.IsIgnited)
                        drawer.DrawLine(posX, posY, posTX, posTY, Color.green);
                    else
                        drawer.DrawDashedLine(posX, posY, posTX, posTY, Color.green, 4, 4);
                }
            }
        }

        /// <summary>
        /// Select and lock
        /// </summary>
        /// <param name="aircraft">Target aircraft</param>
        private void SelectCursorTarget(Aircraft aircraft)
        {
            selectedAircraft = aircraft;

            if (radar.lockedAircraft != selectedAircraft)
            {
                radar.LockAircraft(selectedAircraft, false);
            }
        }

        private void OnRadarLock(Aircraft aircraft)
        {
            if (selectedAircraft != aircraft)
            {
                SelectCursorTarget(aircraft);
            }
        }

        private void DrawCursor(int x0, int y0)
        {
            drawer.DrawRectCenter(x0 - 14, y0, 1, 7, new Color32(0, 255, 0, 255));
            drawer.DrawRectCenter(x0 + 14, y0, 1, 7, new Color32(0, 255, 0, 255));
        }

        private void DrawCursor(Vector2Int pos)
        {
            DrawCursor(pos.x, pos.y);
        }

        private (int, int) TransformWorldToRadar(Vector3 worldPos)
        {
            Vector3 localPos = parentAircraft.transform.InverseTransformPoint(worldPos);
            float dst = Vector3.Distance(parentAircraft.transform.position, worldPos);
            float angle = Mathf.Atan2(localPos.x, localPos.z) * Mathf.Rad2Deg;
            int posX = (int)(resolution / 2 + Mathf.Clamp(angle / radar.displayAngleX, -1, 1) * resolution * 0.5f);
            int posY = (int)(Mathf.Clamp01(dst / maxDrawDistance / 1000) * resolution);

            return (posX, posY);
        }
    }
}