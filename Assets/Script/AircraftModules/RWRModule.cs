using System.Collections;
using AeroSim.UI;
using AeroSim.AeroPhysics;
using UnityEngine;
using AeroSim.Audio;
using System.Collections.Generic;
using System;
using AeroSim.Utils;
using Unity.VisualScripting;

namespace AeroSim.AircraftModules
{
    public class RWRModule : AircraftModule
    {
        public static float defaultDisplayLifetime = 1;

        [Serializable]
        public struct ReceivedObjectData
        {
            public TargetType targetType;

            public GameObject signalTransmitter;
            public string displayText;
            public bool isLockingUs;
            public Vector3 position;
            public float displayLifetime;

            public bool needAlarm;

            public ReceivedObjectData(TargetType targetType, GameObject transmitter, string displayText, bool isLocking, Vector3 position, float displayLifetime, bool needAlarm)
            {
                this.targetType = targetType;
                signalTransmitter = transmitter;
                this.displayText = displayText;
                isLockingUs = isLocking;
                this.position = position;

                this.displayLifetime = displayLifetime;

                this.needAlarm = needAlarm;
            }
        }

        public enum TargetType
        {
            Unknown,
            Aircraft,
            MSL
        }

        public float range = 20;

        //private RadarModule radar; //rwr只是一个接收机，不依赖机载雷达

        public List<ReceivedObjectData> receivedObjects = new List<ReceivedObjectData>();

        private void Start()
        {
            OriginKeeper.onOriginChange += OnOriginChange;
        }

        // Update is called once per frame
        void Update()
        {
            UpdateObjectsLifetime();
        }

        void OnGUI()
        {
            if(parentAircraft.isControlling)
                UpdateObjects();
        }

        // actually it receives object data from other object
        public void TransmittObjectData(TargetType type, GameObject transmitter, string displayText, bool isLocking, Vector3 position, bool needAlarm = true)
        {
            if (!receivedObjects.Exists(x => x.signalTransmitter == transmitter))
            {
                receivedObjects.Add(new ReceivedObjectData(type, transmitter, displayText, isLocking, position, defaultDisplayLifetime, needAlarm));
            }
            else
            {
                int index = receivedObjects.FindIndex(x => x.signalTransmitter == transmitter);
                ReceivedObjectData objData = receivedObjects[index];
                objData.targetType = type;
                objData.displayLifetime = defaultDisplayLifetime;
                objData.needAlarm = needAlarm;
                objData.isLockingUs = isLocking;
                objData.displayText = displayText;
                objData.position = position;
                receivedObjects[index] = objData;
            }
        }

        public bool IsExistedInReceivedObjects(GameObject transmitter)
        {
            return receivedObjects.Exists(x => x.signalTransmitter == transmitter);
        }

        void OnOriginChange(Vector3 delta)
        {
            for(int i = 0; i < receivedObjects.Count; i++)
            {
                ReceivedObjectData obj = receivedObjects[i];
                obj.position += delta;
                receivedObjects[i] = obj;
            }
        }

        void UpdateObjects()
        {
            int rwrAlarmType = -1;
            bool isLocking = false;

            Vector3 fwd = parentAircraft.transform.forward;
            float rwrRotateAngle = Mathf.Atan2(fwd.x, fwd.z);

            for (int i = 0; i < receivedObjects.Count; i++)
            {
                ReceivedObjectData objectData = receivedObjects[i];

                Vector3 deltaPos = objectData.position - parentAircraft.transform.position;
                float angle = Mathf.Atan2(deltaPos.x, deltaPos.y);
                float deltaAngle = angle - rwrRotateAngle;
                float magnitude = Mathf.Clamp(deltaPos.magnitude, 0, range * 1000);
                Vector3 transformedPos = new Vector3(magnitude * Mathf.Sin(deltaAngle), 0, magnitude * Mathf.Cos(deltaAngle));

                // Update RWR alarm type
                if (objectData.isLockingUs && objectData.targetType == TargetType.MSL)
                {
                    isLocking = true;

                    rwrAlarmType = 2;
                }
                else if (objectData.isLockingUs && objectData.targetType == TargetType.Aircraft)
                {
                    isLocking = true;

                    if (rwrAlarmType < 1)
                        rwrAlarmType = 1;

                }
                else if (objectData.needAlarm)
                {
                    if(rwrAlarmType < 0)
                        rwrAlarmType = 0;

                    objectData.needAlarm = false;
                }

                receivedObjects[i] = objectData;
            }

            if (!isLocking && AircraftUI.IsRWRLabelActived())
            {
                AircraftUI.RwrMsg.text = "<color=green>RWR</color>";
                AircraftUI.DisplayRWRMsgBGS(false);

                if (AudioManager.IsPlayingRwr)
                {
                    AudioManager.RWRStop();
                }
            }

            if (rwrAlarmType == 2)
            {
                if (!AircraftUI.IsRWRLabelActived() || AircraftUI.RwrMsg.text != "<color=green>敌导弹</color>")
                {
                    AircraftUI.RwrMsg.text = "<color=green>敌导弹</color>";
                    AircraftUI.DisplayRWRMsgBGS(true);
                }

                AudioManager.RWRMsl();
            }
            else if(rwrAlarmType == 1)
            {
                if (!AircraftUI.IsRWRLabelActived() || AircraftUI.RwrMsg.text != "<color=green>敌跟踪</color>")
                {
                    AircraftUI.RwrMsg.text = "<color=green>敌跟踪</color>";
                    AircraftUI.DisplayRWRMsgBGS(true);
                }

                AudioManager.RWRLock();
            }
            else if(rwrAlarmType == 0)
            {
                AudioManager.RWRScan();
            }
        }

        //void UpdateTargets()
        //{
        //    RectTransform rwr = AircraftUI.RWR;
        //    foreach (Aircraft aircraft in radar.ScannedAircrafts)
        //    {
        //        Vector3 localPos = parentAircraft.transform.InverseTransformPoint(aircraft.transform.position);
        //        float magnitude = localPos.magnitude;
        //        float magnitudeXZ = new Vector3(localPos.x, 0, localPos.z).magnitude;
        //        float mCoeff = magnitude / magnitudeXZ;
        //        if (magnitude < range * 1000 / 2)
        //        {
        //            GUI.Label(
        //                new Rect(
        //                    rwr.position.x + localPos.x * mCoeff / (range * 1000) * rwr.rect.width - 25,
        //                    Screen.height - (rwr.position.y + localPos.z * mCoeff / (range * 1000) * rwr.rect.height + 25),
        //                    50, 50),
        //                $"<color=green>{aircraft.simpifiedName}</color>", targetLabelStyle);
        //        }
        //    }
        //}

        //void UpdateMSLs()
        //{
        //    RectTransform rwr = AircraftUI.RWR;
        //    foreach (Missle missle in radar.ScannedMissles)
        //    {
        //        if(!AircraftUI.IsRWRLabelActived() && radar.IsLocked)
        //        {
        //            AircraftUI.RwrMsg.text = "<color=green>敌导弹</color>";
        //            AircraftUI.DisplayRWRMsgBGS(true);

        //            if(!AudioManager.IsPlayingMsl)
        //            {
        //                AudioManager.RWRMsl();
        //            }
        //        }
        //        else if (AircraftUI.IsRWRLabelActived() && !radar.IsLocked)
        //        {
        //            AircraftUI.RwrMsg.text = "<color=green>RWR</color>";
        //            AircraftUI.DisplayRWRMsgBGS(false);

        //            if (!AudioManager.IsPlayingRwr)
        //            {
        //                AudioManager.RWRStop();
        //            }
        //        }

        //        if (missle.target == parentAircraft.transform)
        //        {
        //            Vector3 localPos = parentAircraft.transform.InverseTransformPoint(missle.transform.position);
        //            float magnitude = localPos.magnitude;
        //            float magnitudeXZ = new Vector3(localPos.x, 0, localPos.z).magnitude;
        //            float mCoeff = magnitude / magnitudeXZ;

        //            if(magnitude > range * 1000 / 2)
        //            {
        //                mCoeff = range * 1000 / 2 / magnitudeXZ;
        //            }
        //            GUI.Label(
        //                new Rect(
        //                    rwr.position.x + localPos.x * mCoeff / (range * 1000) * rwr.rect.width - 25,
        //                    Screen.height - (rwr.position.y + localPos.z * mCoeff / (range * 1000) * rwr.rect.height + 25),
        //                    50, 50),
        //                $"<color=green>MSL</color>", targetLabelStyle);
        //        }
        //    }
        //}

        void UpdateObjectsLifetime()
        {
            //update objects lifetime
            for (int i = receivedObjects.Count - 1; i >= 0; i--)
            {
                ReceivedObjectData objectData = receivedObjects[i];
                objectData.displayLifetime -= Time.deltaTime;
                if (objectData.needAlarm)
                    objectData.needAlarm = false;

                receivedObjects[i] = objectData;

                if(objectData.displayLifetime < Time.deltaTime)
                {
                    receivedObjects.RemoveAt(i);
                }
            }
        }
    }
}