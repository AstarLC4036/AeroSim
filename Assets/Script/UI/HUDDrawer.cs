using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.Utility;
using AeroSim.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static AeroSim.AircraftModules.RadarModule;

namespace AeroSim.UI
{
    public class HUDDrawer : MonoBehaviour
    {
        public class HUDMeshData
        {
            public Mesh mesh;
            private Vector3 direction;
            public Space space;

            public Vector3 Direction
            {
                get {  return direction; }
                set { direction = value.normalized; }
            }

            public HUDMeshData(Mesh mesh, Vector3 direction)
            {
                direction = direction.normalized;
                this.mesh = mesh;
                this.direction = direction;
                space = Space.World;
            }

            public HUDMeshData(Mesh mesh, Vector3 direction, Space space)
            {
                direction = direction.normalized;
                this.mesh = mesh;
                this.direction = direction;
                this.space = space;
            }
        }

        private static HUDDrawer instance;
        public static HUDDrawer Instance => instance;

        public Texture2D vecPointer;
        public Texture2D aimDirPointer;
        public Texture2D dirPointer;
        public Texture2D mslPointer;
        public Texture2D activeMslLockIndicator;
        public RectTransform hudParent;
        public Material hudMaterial;
        public int hudLayer;

        [Header("MSA Radar")]
        public RectTransform msaEadarHorizon;
        public RectTransform msaRadarVertical;

        [Header("AESA Radar")]
        public RectTransform aesaRadarLT;
        public RectTransform aesaRadarLB;
        public RectTransform aesaRadarRT;
        public RectTransform aesaRadarRB;

        [Header("HUD Config")]
        public float veloPointerSize = 8;
        public float aimDirPointerSize = 8;
        public float dirPointerSize = 8;
        public float targetTextSize = 50;
        public float lockTextSize = 50;
        public float lockRectSize = 25;
        public float lockVectorCircleDy = 0;
        public float lockVectorCircleRadius = 15;
        public float lockVectorLength = 10;
        public float mslCursorSize = 50;
        public float lockRingSize = 50;

        [Header("Properties")]
        public List<HUDMeshData> hudMeshes = new List<HUDMeshData>();
        public float hudDistance = 0;
        private HUDMeshData radarRangeMesh;

        private RadarModule radar;
        private MSARadar msaRadar;
        private AESARadar aesaRadar;

        [SerializeField]
        private Material lineMaterial;
        private GUIStyle targetLabelStyle;
        private GUIStyle lockLabelStyle;

        private Vector2 radarPosDelta;
        private float aesaPosX;
        private float aesaPosY;
        private float rollAngle;

        private bool frontDisplay = true;

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            targetLabelStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                font = AircraftUI.RwrFont,
                richText = true
            };
            lockLabelStyle = new GUIStyle
            {
                alignment = TextAnchor.LowerLeft,
                fontSize = 20,
                font = AircraftUI.RwrFont,
                richText = true
            };
        }

        public void SetRadar(RadarModule radar)
        {
            this.radar = radar;
            msaRadar = null;
            aesaRadar = null;

            if (radar.radarType == RadarModule.RadarType.MSA)
            {
                msaRadar = (MSARadar)radar;
                SetMsaHud();

                //TODO: MSA Radar HUD
                if(radarRangeMesh != null)
                {
                    RemoveHUDMesh(radarRangeMesh);
                }
                radarRangeMesh = CreateHUDRect(radar.currentScanAngleX, radar.currentScanAngleY, Vector3.forward);
                radarRangeMesh.space = Space.Self;
            }
            else if (radar.radarType == RadarModule.RadarType.AESA)
            {
                aesaRadar =(AESARadar)radar;
                SetAesaHud();

                if (radarRangeMesh != null)
                {
                    RemoveHUDMesh(radarRangeMesh);
                }
                radarRangeMesh = CreateHUDRect(radar.currentScanAngleX, radar.currentScanAngleY, Vector3.forward);
                radarRangeMesh.space = Space.Self;
            }
        }

        /// <summary>
        /// Create a circle on HUD, remove the Mesh manually to stop the display
        /// </summary>
        /// <param name="angle">Angle in degrees</param>
        /// <returns>Referenced mesh data</returns>
        public HUDMeshData CreateHUDCircle(float angle, Vector3 direction)
        {
            float radius = Mathf.Tan(angle * Mathf.Deg2Rad) * hudDistance;
            Mesh mesh = WireframeMeshFactory.CreateThickCircle(radius, 0.1f);
            HUDMeshData data = new HUDMeshData(mesh, direction);
            hudMeshes.Add(data);
            return data;
        }

        /// <summary>
        /// Create a rect on HUD, remove the Mesh manually to stop the display
        /// </summary>
        /// <param name="w">Width (Angle in degrees)</param>
        /// <param name="h">Height (Angle in degrees)</param>
        /// <returns>Referenced mesh data</returns>
        public HUDMeshData CreateHUDRect(float w, float h, Vector3 direction)
        {
            float width = Mathf.Tan(w * Mathf.Deg2Rad) * hudDistance;
            float height = Mathf.Tan(h * Mathf.Deg2Rad) * hudDistance;
            Mesh mesh = WireframeMeshFactory.CreateThickRectangle(width, height, 0.1f);
            HUDMeshData data = new HUDMeshData(mesh, direction);
            hudMeshes.Add(data);
            return data;
        }

        /// <summary>
        /// Remove the displaying HUD Mesh
        /// </summary>
        /// <param name="mesh">Mesh</param>
        /// <returns>Is the mesh removed successfully</returns>
        public bool RemoveHUDMesh(HUDMeshData data)
        {
            return hudMeshes.Remove(data);
        }

        public void HideAllHud()
        {
            msaEadarHorizon.gameObject.SetActive(false);
            msaRadarVertical.gameObject.SetActive(false);

            aesaRadarLB.gameObject.SetActive(false);
            aesaRadarLT.gameObject.SetActive(false);
            aesaRadarRB.gameObject.SetActive(false);
            aesaRadarRT.gameObject.SetActive(false);
        }

        public void SetMsaHud()
        {
            msaEadarHorizon.gameObject.SetActive(true);
            msaRadarVertical.gameObject.SetActive(true);

            aesaRadarLB.gameObject.SetActive(false);
            aesaRadarLT.gameObject.SetActive(false);
            aesaRadarRB.gameObject.SetActive(false);
            aesaRadarRT.gameObject.SetActive(false);
        }

        public void SetAesaHud()
        {
            msaEadarHorizon.gameObject.SetActive(false);
            msaRadarVertical.gameObject.SetActive(false);

            aesaRadarLB.gameObject.SetActive(true);
            aesaRadarLT.gameObject.SetActive(true);
            aesaRadarRB.gameObject.SetActive(true);
            aesaRadarRT.gameObject.SetActive(true);
        }

        // Update is called once per frame
        void Update()
        {
            radarPosDelta = Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.transform.forward);

            rollAngle = -Aircraft.main.transform.eulerAngles.z;
            //Vector3 dirVecOnScreen = Aircraft.main.Velocity.magnitude > 0.1 ? Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.Velocity.normalized) : Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.transform.right);

            if (msaRadar != null)
            {
                Vector3 radarAngles = msaRadar.PointerPosOnScreen;

                float positionMax = MathUtility.TransformAngle(msaRadar.currentScanAngleX, Camera.main.fieldOfView, Camera.main.pixelHeight);
                float positionX = MathUtility.TransformAngle(radarAngles.x, Camera.main.fieldOfView, Camera.main.pixelHeight);
                float positionY = MathUtility.TransformAngle(radarAngles.y, Camera.main.fieldOfView, Camera.main.pixelHeight);
                msaEadarHorizon.localPosition = new Vector3(positionX, positionY, 0);
                msaRadarVertical.localPosition = new Vector3(positionMax + msaEadarHorizon.rect.size.x / 2 + 10, positionY, 0);

                hudParent.localRotation = Quaternion.Euler(0, 0, -rollAngle);
            }
            else if(aesaRadar != null)
            {
                float positionX = MathUtility.TransformAngle(aesaRadar.currentScanAngleX, Camera.main.fieldOfView, Camera.main.pixelHeight);
                float positionY = MathUtility.TransformAngle(aesaRadar.currentScanAngleY, Camera.main.fieldOfView, Camera.main.pixelHeight);
                if (radar.radarMode == RadarMode.HMD && radar.radarCarrier == RadarCarrier.Aircraft)
                {
                    Vector3 localDir = radar.hmdPointer.forward;
                    Vector3 radarFwd = radar.transform.forward;
                    float angleX = (Mathf.Atan2(localDir.x, localDir.z) - Mathf.Atan2(radarFwd.x, radarFwd.z)) * Mathf.Rad2Deg;
                    float angleY = (Mathf.Asin(Mathf.Clamp(localDir.y, -1, 1)) - Mathf.Asin(Mathf.Clamp(radarFwd.y, -1, 1))) * Mathf.Rad2Deg;
                    float w = MathUtility.TransformAngle(radar.currentScanAngleX, Camera.main.fieldOfView, Camera.main.pixelHeight);
                    float h = MathUtility.TransformAngle(radar.currentScanAngleY, Camera.main.fieldOfView, Camera.main.pixelHeight);
                    float posX1 = MathUtility.TransformAngle(angleX, Camera.main.fieldOfView, Camera.main.pixelHeight);
                    float posX2 = MathUtility.TransformAngle(angleX, Camera.main.fieldOfView, Camera.main.pixelHeight);
                    float posY1 = MathUtility.TransformAngle(angleY, Camera.main.fieldOfView, Camera.main.pixelHeight);
                    float posY2 = MathUtility.TransformAngle(angleY, Camera.main.fieldOfView, Camera.main.pixelHeight);
                    //aesaPosX = positionX / Screen.width;
                    //aesaPosY = positionY / Screen.height;
                    aesaRadarRT.localPosition = new Vector3(posX2 + w, posY2 + h);
                    aesaRadarRB.localPosition = new Vector3(posX2 + w, posY1 - h);
                    aesaRadarLT.localPosition = new Vector3(posX1 - w, posY2 + h);
                    aesaRadarLB.localPosition = new Vector3(posX1 - w, posY1 - h);

                    hudParent.localRotation = Quaternion.Euler(0, 0, 0);
                }
                else
                {
                    //aesaPosX = positionX / Screen.width;
                    //aesaPosY = positionY / Screen.height;
                    aesaRadarRT.localPosition = new Vector3(positionX, positionY);
                    aesaRadarRB.localPosition = new Vector3(positionX, -positionY);
                    aesaRadarLT.localPosition = new Vector3(-positionX, positionY);
                    aesaRadarLB.localPosition = new Vector3(-positionX, -positionY);

                    hudParent.localRotation = Quaternion.Euler(0, 0, -rollAngle);
                }
            }
            hudParent.position = radarPosDelta;

            //UpdateHUDMesh();
            //DrawHUDMesh();

            //pitchLadderBars.ForEach((Bar bar) => {
            //    float rollAngle = currentAircraft.transform.eulerAngles.x;
            //    float covertedAngle = MathUtility.CovertAngle(Mathf.DeltaAngle(currentAircraft.transform.eulerAngles.z, bar.angle));
            //    float position = MathUtility.TransformAngle(covertedAngle, Camera.main.fieldOfView, Camera.main.pixelHeight);

            //    if (position >= pitchLadderMinY && position <= pitchLadderMaxY)
            //    {
            //        if (!bar.UIObject.activeSelf)
            //            bar.UIObject.SetActive(true);
            //        RectTransform barTransform = bar.UIObject.GetComponent<RectTransform>();
            //        barTransform.localPosition = new Vector3(0, position, 0);
            //        pitchLadderUIParent.localRotation = Quaternion.Euler(0, 0, -rollAngle);
            //    }
            //    else
            //    {
            //        if (bar.UIObject.activeSelf)
            //            bar.UIObject.SetActive(false);
            //    }
            //});
        }

        void UpdateHUDMesh()
        {
            switch(radar.radarMode)
            {
                case RadarMode.SRC:
                    break;
            }
        }

        void DrawHUDMesh()
        {
            Camera cam = Camera.main;
            Vector3 position = cam.transform.position;
            Quaternion rotation = cam.transform.rotation;
            foreach(HUDMeshData data in hudMeshes)
            {
                if (data.space == Space.Self)
                {
                    Graphics.DrawMesh(data.mesh, position + data.Direction * hudDistance, rotation, hudMaterial, hudLayer);
                }
                else if(data.space == Space.World)
                {
                    Graphics.DrawMesh(data.mesh, position + data.Direction * hudDistance, rotation, hudMaterial, hudLayer);
                }
            }
        }

        void OnGUI()
        {
            //float posX = radarPosDelta.x / Screen.width;
            //float posY = radarPosDelta.y / Screen.width;

            if (AircraftUI.isTargetingViewEnabled)
                return;

            // Basic screen cursor
            if (Aircraft.main.Velocity.magnitude > 0.1)
            {
                Vector3 vecOnScreen = Aircraft.main.Velocity.magnitude > 0.1 ? Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.Velocity.normalized) : Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.transform.right);
                bool isFront = vecOnScreen.z >= 0;
                if (frontDisplay != isFront)
                {
                    frontDisplay = isFront;
                }
                if (isFront)
                    GUI.DrawTexture(Utilities.CalcucateTextureScreenPos(vecOnScreen, veloPointerSize), vecPointer);
            }

            Vector3 aimDirOnScreen = Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.targetDir * 10);
            if(aimDirOnScreen.z >= 0)
            {
                GUI.DrawTexture(Utilities.CalcucateTextureScreenPos(aimDirOnScreen, aimDirPointerSize), aimDirPointer);
            }

            Vector3 dirOnScreen = Camera.main.WorldToScreenPoint(Camera.main.transform.position + Aircraft.main.transform.forward * 10);
            if (dirOnScreen.z >= 0)
            {
                GUI.DrawTexture(Utilities.CalcucateTextureScreenPos(dirOnScreen, dirPointerSize), dirPointer);
            }

            // Radar HUD
            if (aesaRadar != null)
            {
                foreach (Aircraft aircraft in aesaRadar.ScannedAircrafts)
                {
                    Vector3 targetPosOnScreen = Camera.main.WorldToScreenPoint(aircraft.transform.position + Vector3.down * 3);
                    if (targetPosOnScreen.z >= 0)
                    {
                        if (aircraft != aesaRadar.lockedAircraft)
                        {
                            GUI.Label(new Rect(targetPosOnScreen.x - targetTextSize / 2, Screen.height - targetPosOnScreen.y - targetTextSize / 2, targetTextSize, targetTextSize),
                                $"<size=24><color=green>{aircraft.aircraftName}</color></size>\n<size=16><color=green>{(int)(Vector3.Distance(Aircraft.main.transform.position, aircraft.transform.position) / 1000 * 10) / 10f} km</color></size>",
                                targetLabelStyle);
                        }
                        else //locked aircraft
                        {
                            Vector3 targetVeloOnScreen = Camera.main.WorldToScreenPoint(aircraft.transform.position + Vector3.down * 3 + aircraft.Velocity.normalized);
                            Vector2 targetVeloNorm = (targetVeloOnScreen - targetPosOnScreen).normalized;
                            Vector3 targetPosScrPercent = new Vector3(targetPosOnScreen.x / Screen.width, targetPosOnScreen.y / Screen.height, targetPosOnScreen.z);
                            float rectX = lockRectSize / Screen.width;
                            float rectY = lockRectSize / Screen.height;

                            GUI.Label(new Rect(targetPosOnScreen.x + lockTextSize / 2 + lockRectSize / 2, Screen.height - targetPosOnScreen.y, lockTextSize, lockRectSize),
                                $"<size=16><color=green>{aircraft.aircraftName}</color></size>\n<size=16><color=green>{(int)(Vector3.Distance(Aircraft.main.transform.position, aircraft.transform.position) / 1000 * 10) / 10f} km</color></size>",
                                lockLabelStyle);

                            GL.PushMatrix();
                            GL.LoadOrtho();
                            lineMaterial.SetPass(0);

                            // Radar lock cursor
                            GL.Begin(GL.LINE_STRIP);
                            GL.Color(Color.green);

                            GL.Vertex3(targetPosScrPercent.x - rectX, targetPosScrPercent.y - rectY, 0);
                            GL.Vertex3(targetPosScrPercent.x + rectX, targetPosScrPercent.y - rectY, 0);
                            GL.Vertex3(targetPosScrPercent.x + rectX, targetPosScrPercent.y + rectY, 0);
                            GL.Vertex3(targetPosScrPercent.x - rectX, targetPosScrPercent.y + rectY, 0);
                            GL.Vertex3(targetPosScrPercent.x - rectX, targetPosScrPercent.y - rectY, 0);

                            GL.End();

                            // Radar velo drawer
                            GL.Begin(GL.LINE_STRIP);
                            GL.Color(Color.green);
                            for (int angle = 0; angle <= 360; angle++)
                            {
                                GL.Vertex3(
                                    targetPosScrPercent.x - (Mathf.Cos(angle * Mathf.Deg2Rad) * lockVectorCircleRadius) / Screen.width,
                                    targetPosScrPercent.y - (lockRectSize + lockVectorCircleDy + Mathf.Sin(angle * Mathf.Deg2Rad) * lockVectorCircleRadius) / Screen.height,
                                    0);
                            }
                            GL.End();

                            GL.Begin(GL.LINES);
                            GL.Color(Color.green);
                            GL.Vertex3(
                                targetPosScrPercent.x - (-targetVeloNorm.x * lockVectorCircleRadius) / Screen.width,
                                targetPosScrPercent.y - (lockRectSize + lockVectorCircleDy - targetVeloNorm.y * lockVectorCircleRadius) / Screen.height,
                            0);
                            GL.Vertex3(
                                targetPosScrPercent.x - (-targetVeloNorm.x * (lockVectorCircleRadius + lockVectorLength)) / Screen.width,
                                targetPosScrPercent.y - (lockRectSize + lockVectorCircleDy - targetVeloNorm.y * (lockVectorCircleRadius + lockVectorLength)) / Screen.height,
                                0);
                            GL.End();

                            GL.PopMatrix();
                        }
                    }
                }

                if(Aircraft.main.mslManager != null && Aircraft.main.mslManager.currentMissle != null && Aircraft.main.mslManager.currentMissle.lockState == Missile.LockState.Locked)
                {
                    Vector3 mslPosOnScreen = Camera.main.WorldToScreenPoint(Aircraft.main.mslManager.currentMissle.TargetPosition);
                    if (mslPosOnScreen.z > 0)
                    {
                        GUI.DrawTexture(new Rect(
                            mslPosOnScreen.x - lockRingSize / 2,
                            Screen.height - mslPosOnScreen.y - lockRingSize / 2,
                            lockRingSize, lockRingSize),
                            activeMslLockIndicator);
                    }
                }

                if (!radar.IsTracking)
                {
                    GL.PushMatrix();
                    GL.LoadOrtho();

                    lineMaterial.SetPass(0);

                    GL.Begin(GL.LINE_STRIP);
                    GL.Color(Color.green);

                    //only works in a limited condition that scan area is a square
                    /*
                    float rollSine = Mathf.Sin((rollAngle + 45) * Mathf.Deg2Rad);
                    float rollCosine = Mathf.Cos((rollAngle + 45) * Mathf.Deg2Rad);

                    GL.Vertex3(0.5f + aesaPosX * rollCosine, 0.5f - aesaPosY * rollSine, 0);
                    GL.Vertex3(0.5f - aesaPosX * rollSine, 0.5f - aesaPosY * rollCosine, 0);
                    GL.Vertex3(0.5f - aesaPosX * rollCosine, 0.5f + aesaPosY * rollSine, 0);
                    GL.Vertex3(0.5f + aesaPosX * rollSine, 0.5f + aesaPosY * rollCosine, 0);
                    GL.Vertex3(0.5f + aesaPosX * rollCosine, 0.5f - aesaPosY * rollSine, 0);
                    */

                    /*
                    float angleX = Mathf.Atan2(aesaRadar.scanAngleY, aesaRadar.scanAngleX) * Mathf.Rad2Deg;

                    float semiDiagonalLen = Mathf.Sqrt(aesaPosX * aesaPosX + aesaPosY * aesaPosY);

                    float rollSine1 = Mathf.Sin((rollAngle + angleX) * Mathf.Deg2Rad);
                    float rollCosine1 = Mathf.Cos((rollAngle + angleX) * Mathf.Deg2Rad);

                    float rollSine2 = Mathf.Sin((rollAngle + 180 - angleX) * Mathf.Deg2Rad);
                    float rollCosine2 = Mathf.Cos((rollAngle + 180 - angleX) * Mathf.Deg2Rad);

                    GL.Vertex3(posX + semiDiagonalLen * rollCosine1, posY - semiDiagonalLen * rollSine1, 0);
                    GL.Vertex3(posX + semiDiagonalLen * rollCosine2, posY - semiDiagonalLen * rollSine2, 0);
                    GL.Vertex3(posX - semiDiagonalLen * rollCosine1, posY + semiDiagonalLen * rollSine1, 0);
                    GL.Vertex3(posX - semiDiagonalLen * rollCosine2, posY + semiDiagonalLen * rollSine2, 0);
                    GL.Vertex3(posX + semiDiagonalLen * rollCosine1, posY - semiDiagonalLen * rollSine1, 0);
                    */

                    GL.Vertex3(aesaRadarLT.position.x / Screen.width, aesaRadarLT.position.y / Screen.height, 0);
                    GL.Vertex3(aesaRadarRT.position.x / Screen.width, aesaRadarRT.position.y / Screen.height, 0);
                    GL.Vertex3(aesaRadarRB.position.x / Screen.width, aesaRadarRB.position.y / Screen.height, 0);
                    GL.Vertex3(aesaRadarLB.position.x / Screen.width, aesaRadarLB.position.y / Screen.height, 0);
                    GL.Vertex3(aesaRadarLT.position.x / Screen.width, aesaRadarLT.position.y / Screen.height, 0);

                    GL.End();
                    GL.PopMatrix();
                }
            }
        }
    }
}