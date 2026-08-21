using AeroSim.AeroPhysics;
using AeroSim.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class AESARadar : RadarModule
    {
        private float transmissonDelay = 1;
        private float transmitTimer = 0;

        protected override void Awake()
        {
            base.Awake();
            defaultModes = new RadarMode[] { RadarMode.TWS, RadarMode.ACM, RadarMode.HMD };
        }

        public override void UpdateScan()
        {
            base.UpdateScan();
            if (!IsTracking)
            {
                foreach (Aircraft aircraft in AircraftManager.Aircrafts)
                {
                    if (aircraft == parentAircraft)
                        continue;

                    Vector3 localPos;
                    if (radarMode == RadarMode.HMD && isControlling && radarCarrier == RadarCarrier.Aircraft)
                    {
                        localPos = hmdPointer.InverseTransformPoint(aircraft.transform.position);
                    }
                    else
                    {
                        localPos = transform.InverseTransformPoint(aircraft.transform.position);
                    }

                    float maxDistX = localPos.z * Mathf.Tan(currentScanAngleX * Mathf.Deg2Rad);
                    float maxDistY = localPos.z * Mathf.Tan(currentScanAngleY * Mathf.Deg2Rad);
                    if (localPos.x >= -maxDistX
                       && localPos.x <= maxDistX
                       && localPos.y >= -maxDistY
                       && localPos.y <= maxDistY
                       && localPos.z >= 0
                       && localPos.magnitude < maxScanRange * 1000)
                    {
                        if (!scannedAircrafts.Exists(x => x == aircraft))
                            scannedAircrafts.Add(aircraft);
                    }
                    else if (scannedAircrafts.Exists(x => x == aircraft))
                    {
                        scannedAircrafts.Remove(aircraft);
                    }
                }

                bool isLockedM = false;
                foreach (Missile missle in AircraftManager.Missles)
                {
                    if (Vector3.Distance(transform.position, missle.transform.position) <= maxScanRange * 1000)
                    {
                        if (!scannedMissles.Exists(x => x == missle))
                        {
                            scannedMissles.Add(missle);
                        }

                        if (radarCarrier == RadarCarrier.Aircraft)
                        {
                            if (missle.target == parentAircraft.transform)
                            {
                                isLockedM = true;
                            }
                        }
                    }
                    else if (scannedMissles.Exists(x => x == missle))
                    {
                        scannedMissles.Remove(missle);
                    }
                }
                isLocked = isLockedM;
            }
            else
            {
                Vector3 localPos = transform.InverseTransformPoint(lockedAircraft.transform.position);
                float maxDistX = localPos.z * Mathf.Tan(currentScanAngleX * Mathf.Deg2Rad);
                float maxDistY = localPos.z * Mathf.Tan(currentScanAngleY * Mathf.Deg2Rad);
                if (localPos.x >= -maxDistX
                   && localPos.x <= maxDistX
                   && localPos.y >= -maxDistY
                   && localPos.y <= maxDistY
                   && localPos.z >= 0
                   && localPos.magnitude < maxScanRange * 1000)
                {
                    if (!scannedAircrafts.Exists(x => x == lockedAircraft))
                        scannedAircrafts.Add(lockedAircraft);
                }
                else if (scannedAircrafts.Exists(x => x == lockedAircraft))
                {
                    scannedAircrafts.Remove(lockedAircraft);
                }
            }
        }

        protected override void OnModeChanged(RadarMode mode)
        {
            base.OnModeChanged(mode);
            switch (mode)
            {
                case RadarMode.STT:
                    HUDDrawer.Instance.HideAllHud();
                    currentScanAngleX = maxScanAngleX;
                    currentScanAngleY = maxScanAngleY;
                    scannedAircrafts.Clear();
                    break;
            }
        }

        protected override void OnLostLock()
        {
            base.OnLostLock();
            SetMode(RadarMode.TWS);
        }

        protected override void QuitHmdMode()
        {
            SetMode(RadarMode.TWS);
        }

        public override void LockAircraft(Aircraft aircraft, bool switchMode = false)
        {
            base.LockAircraft(aircraft);
            if(switchMode && aircraft != null)
                SetMode(RadarMode.STT);
        }

        public override void UpdateTransmisson()
        {
            transmitTimer -= Time.deltaTime;
            if (transmitTimer < 0)
            {
                transmitTimer = transmissonDelay;
                foreach (Aircraft aircraft in scannedAircrafts)
                {
                    if(aircraft == lockedAircraft)
                        continue;

                    if (aircraft.rwr != null && UnityEngine.Random.value <= 0.1f)
                    {
                        float dst = Vector3.Distance(transform.position, aircraft.transform.position);

                        if (dst > 20000)
                        {
                            aircraft.rwr.TransmittObjectData(RWRModule.TargetType.Unknown, gameObject, displayName, false, transform.position);
                        }
                        else if(dst > 5000)
                        {
                            aircraft.rwr.TransmittObjectData(RWRModule.TargetType.Aircraft, gameObject, displayName, false, transform.position);
                        }
                    }
                }
            }

            foreach (Aircraft aircraft in scannedAircrafts)
            {
                if (aircraft == lockedAircraft)
                    continue;

                if (aircraft.rwr != null)
                {
                    float dst = Vector3.Distance(transform.position, aircraft.transform.position);

                    if (dst <= 5000)
                    {
                        //bool needAlarm = aircraft.rwr.IsExistedInReceivedObjects(gameObject);
                        aircraft.rwr.TransmittObjectData(RWRModule.TargetType.Aircraft, gameObject, displayName, false, transform.position, true);
                    }
                }
            }

            if(lockedAircraft != null && lockedAircraft.rwr != null)
                lockedAircraft.rwr.TransmittObjectData(RWRModule.TargetType.Aircraft, gameObject, displayName, true, transform.position, false);
        }
    }
}
