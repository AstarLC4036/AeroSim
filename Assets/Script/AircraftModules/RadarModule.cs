using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using AeroSim.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class RadarModule : MonoBehaviour
    {
        public enum RadarType
        {
            MSA,
            AESA
        }

        public enum RadarMode
        {
            SRC, // Search
            TRK, // Track (Signle Target Track)
            STT, // Signle Target Track (AESA)
            TWS, // Track While Scan (AESA)
            ACM, // Air Combat Maneuvering
            HMD, // Helmet-Mounted Display
            VS,  // Velocity Search
        }

        public enum RadarProcessing
        {
            None,
            PD //Pulse Doppler
        }

        public enum RadarCarrier
        {
            Aircraft,
            Missle,
            Ground,
        }

        [Header("Radar Config")]
        public string displayName = "AI";
        public float maxScanAngleX = 60;
        public float maxScanAngleY = 30;
        public float hmdScanAngleX = 20;
        public float hmdScanAngleY = 20;
        public float acmScanAngleX = 30;
        public float acmScanAngleY = 30;
        public float currentScanAngleX;
        public float currentScanAngleY;
        public float displayAngleX;
        public float displayAngleY;
        public float deltaAngleX;
        public float deltaAngleY;
        public bool isRadarActived = true;
        public float maxScanRange = 400;
        public float lostLockTimeout = 3;
        public RadioBand radarBand = RadioBand.X;
        public RadarType radarType;
        public RadarMode radarMode;
        public RadarProcessing radarProcessing;
        public RadarCarrier radarCarrier;
        protected Aircraft parentAircraft;

        public Transform hmdPointer;

        [Header("Input")]
        protected int currentModeIndex = 0;
        protected RadarMode[] defaultModes;

        [Header("Status")]
        public bool IsTracking => radarMode == RadarMode.TRK || radarMode == RadarMode.STT; // track single target.

        [SerializeField]
        protected List<Aircraft> scannedAircrafts = new List<Aircraft>();
        public List<Aircraft> ScannedAircrafts => scannedAircrafts;
        protected List<Missile> scannedMissles = new List<Missile>();
        public List<Missile> ScannedMissles => scannedMissles;
        public Aircraft lockedAircraft;
        protected List<Missile> trackingMsls = new List<Missile>();

        protected bool isLocked = false;
        public bool IsLocked => isLocked;

        public Action<Aircraft> onLock = (aircraft) => { };
        //protected List<Aircraft> lockedAircrafts = new List<Aircraft>();
        //public List<Aircraft> LockedAircrafts => lockedAircrafts;

        [SerializeField]
        protected bool isControlling = false;

        [SerializeField]
        private bool isLockedTargetOutOfRange = false;
        [SerializeField]
        private float unlockTimer = 0;

        protected virtual void Awake()
        {
            displayAngleX = currentScanAngleX = maxScanAngleX;
            displayAngleY = currentScanAngleY = maxScanAngleY;
        }

        public void Init(object carrier)
        {
            switch (radarCarrier)
            {
                case RadarCarrier.Aircraft:
                    parentAircraft = (Aircraft)carrier;
                    displayName = parentAircraft.simpifiedName;
                    break;

                case RadarCarrier.Missle:
                    break;

                case RadarCarrier.Ground:
                    break;
            }

            //init radar status
            switch (radarType)
            {
                case RadarType.MSA:
                    SetMode(RadarMode.SRC);
                    break;
                case RadarType.AESA:
                    SetMode(RadarMode.TWS);
                    break;
            }

            radarProcessing = RadarProcessing.PD;

            switch (radarCarrier)
            {
                case RadarCarrier.Aircraft:
                    isControlling = parentAircraft.isControlling;
                    break;

                case RadarCarrier.Missle:
                    break;

                case RadarCarrier.Ground:
                    break;
            }
        }

        private void Update()
        {
            UpdateInput();
        }

        private void FixedUpdate()
        {
            UpdateScan();
            UpdateTransmisson();
        }

        public virtual void UpdateScan()
        {
            if (radarMode == RadarMode.HMD && isControlling && radarCarrier == RadarCarrier.Aircraft)
            {
                Vector3 dir = GetClampedHmdWorldDirection(Camera.main.transform.forward);
                hmdPointer.LookAt(hmdPointer.position + dir * 50);
            }
        }

        protected Vector3 GetClampedHmdWorldDirection(Vector3 hmdWorldDir)
        {
            // 1. 转到本地坐标系
            Vector3 localDir = transform.InverseTransformDirection(hmdWorldDir).normalized;

            // 2. 从本地方向计算方位和俯仰
            float azimuth = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;   // 左右
            float elevation = Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg; // 上下

            // 3. 钳制
            azimuth = Mathf.Clamp(azimuth, -maxScanAngleX, maxScanAngleX);
            elevation = Mathf.Clamp(elevation, -maxScanAngleY, maxScanAngleY);

            // 4. 重构本地方向（俯仰：绕 X 轴；方位：绕 Y 轴）
            float azRad = azimuth * Mathf.Deg2Rad;
            float elRad = elevation * Mathf.Deg2Rad;
            Vector3 clampedLocalDir = new Vector3(
                Mathf.Sin(azRad) * Mathf.Cos(elRad),
                Mathf.Sin(elRad),
                Mathf.Cos(azRad) * Mathf.Cos(elRad)
            ).normalized;

            // 5. 转回世界方向
            return transform.TransformDirection(clampedLocalDir);
        }

        public void UpdateInput()
        {
            if (isControlling)
            {
                UpdateLock();
                UpdateMode();
            }
        }

        // lock target
        private void UpdateLock()
        {
            if (lockedAircraft != null)
            {
                bool isTargetInRange = scannedAircrafts.Exists(x => x == lockedAircraft);
                if (isLockedTargetOutOfRange == isTargetInRange)
                {
                    if(!isTargetInRange)
                    {
                        unlockTimer = lostLockTimeout;
                    }
                    isLockedTargetOutOfRange = !isTargetInRange;
                }

                if (isLockedTargetOutOfRange)
                {
                    unlockTimer -= Time.fixedDeltaTime;
                    if (unlockTimer < 0)
                    {
                        isLockedTargetOutOfRange = false;
                        LockAircraft(null);
                        OnLostLock();
                    }
                }
            }

            if (Keybindings.radarHmdLockDown || Keybindings.radarTwsLockDown)
            {
                if (Keybindings.radarHmdLockDown)
                {
                    //Refresh status
                    if (radarMode != RadarMode.HMD)
                    {
                        SetMode(RadarMode.HMD);
                        UpdateScan();
                    }
                    else
                    {
                        QuitHmdMode();
                    }
                }

                if (scannedAircrafts.Count == 0)
                    return;

                Aircraft target = null;
                Vector3 cursorPos = FlightController.IsOperationMode ? Input.mousePosition : Camera.main.WorldToScreenPoint(Camera.main.transform.position + Camera.main.transform.forward * 10);
                float dst = float.MaxValue;
                foreach (var aircraft in scannedAircrafts)
                {
                    Vector2 posOnScr = Camera.main.WorldToScreenPoint(aircraft.transform.position);
                    float dstToMouse = Vector3.Distance(posOnScr, cursorPos);

                    if(dstToMouse < dst)
                    {
                        dst = dstToMouse;
                        target = aircraft;
                    }
                }

                if (target != null)
                {
                    if (Keybindings.radarHmdLockDown)
                    {
                        LockAircraft(target, true);
                    }
                    if (Keybindings.radarTwsLockDown)
                    {
                        LockAircraft(target, false);
                    }
                }
            }
        }

        protected virtual void UpdateMode()
        {
            if(Keybindings.radarNextModeDown)
            {
                if (defaultModes.Contains(radarMode))
                {
                    currentModeIndex = Array.IndexOf(defaultModes, radarMode);
                    currentModeIndex = (currentModeIndex + 1) % defaultModes.Length;
                    SetMode(defaultModes[currentModeIndex]);
                }
                else
                {
                    currentModeIndex = 0;
                    SetMode(defaultModes[currentModeIndex]);
                }
            }
        }

        public virtual void UpdateTransmisson()
        {
            //also, nothing here
        }

        public void SetMode(RadarMode mode)
        {
            OnModeChanged(mode);
            radarMode = mode;
            if (isControlling && radarCarrier == RadarCarrier.Aircraft)
            {
                RadarHUDDrawer.UpdateStatusLabel();
            }
        }

        public virtual void LockAircraft(Aircraft aircraft, bool switchMode = false)
        {
            lockedAircraft = aircraft;
            onLock(lockedAircraft);
        }

        protected virtual void OnLostLock()
        {
            
        }

        protected virtual void QuitHmdMode()
        {

        }

        protected virtual void OnModeChanged(RadarMode mode)
        {
            bool isSameMode = false;
            if (mode == radarMode)
                isSameMode = true;

            //没想好
            if (isSameMode)
                return;

            switch (mode)
            {
                case RadarMode.TWS:
                    displayAngleX = currentScanAngleX = maxScanAngleX;
                    displayAngleY = currentScanAngleY = maxScanAngleY;
                    ShowHUD();
                    break;
                case RadarMode.SRC:
                    displayAngleX = currentScanAngleX = maxScanAngleX;
                    displayAngleY = currentScanAngleY = maxScanAngleY;
                    ShowHUD();
                    onLock(null);
                    break;
                case RadarMode.HMD:
                    currentScanAngleX = hmdScanAngleX;
                    currentScanAngleY = hmdScanAngleY;
                    displayAngleX = maxScanAngleX;
                    displayAngleY = maxScanAngleY;
                    onLock(null);
                    break;
                case RadarMode.ACM:
                    currentScanAngleX = acmScanAngleX;
                    currentScanAngleY = acmScanAngleY;
                    displayAngleX = maxScanAngleX;
                    displayAngleY = maxScanAngleY;
                    onLock(null);
                    break;
            }
        }

        void ShowHUD()
        {
            if (radarType == RadarType.MSA)
            {
                HUDDrawer.Instance.SetMsaHud();
            }
            else if (radarType == RadarType.AESA)
            {
                HUDDrawer.Instance.SetAesaHud();
            }
        }

        //Debug
        //void OnGUI()
        //{
        //    Vector3 mousePos = Input.mousePosition;
        //    foreach (var aircraft in scannedAircrafts)
        //    {
        //        Vector3 posOnScr = Camera.main.WorldToScreenPoint(aircraft.transform.position);
        //        GUI.Label(new Rect(posOnScr, new Vector2(50, 50)), "aircraft");
        //    }
        //    GUI.Label(new Rect(mousePos, new Vector2(50, 50)), "mouse");
        //}
    }
}