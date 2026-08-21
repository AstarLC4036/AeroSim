using AeroSim.AeroPhysics;
using AeroSim.UI;
using System.Collections.Generic;
using UnityEngine;
using static AeroSim.AircraftModules.RWRModule;

namespace AeroSim.AircraftModules
{
    public class MAWModule : AircraftModule
    {
        public enum MAWType
        {
            None,
            UV,
            IR,
            IR_UV,
            Radar
        }

        public static Dictionary<MAWType, float> distanceModePair = new Dictionary<MAWType, float>
        {
            { MAWType.UV, 3 }, //kilometres
            { MAWType.IR,  6 },
            { MAWType.IR_UV, 9 },
            { MAWType.Radar, 7},
            //{ Missile.MissileSize.Curise, 2.0f}
        };


        public static Dictionary<Missile.MissileSize, float> distanceWeightPair = new Dictionary<Missile.MissileSize, float>
        {
            { Missile.MissileSize.Small, 0.6f },
            { Missile.MissileSize.Medium, 1.0f },
            { Missile.MissileSize.Large, 1.5f },
            { Missile.MissileSize.AntiRadiation, 1.3f},
            //{ Missile.MissileSize.Curise, 2.0f}
        };

        public MAWType type = MAWType.None;
        public List<Missile> mslDetected = new List<Missile>();

        private void FixedUpdate()
        {
            foreach(Missile msl in AircraftManager.Missles)
            {
                bool detected = IsDetectable(msl);
                if (detected && !mslDetected.Exists(x => x == msl))
                {
                    mslDetected.Add(msl);
                }
                else if(!detected && mslDetected.Exists(x => x == msl))
                {
                    mslDetected.Remove(msl);
                }
            }
        }

        public bool IsDetectable(Missile msl)
        {
            if (!msl.IsIgnited)
                return false;

            float dst = Vector3.Distance(msl.transform.position, parentAircraft.transform.position);
            float weighedMaxDst = distanceModePair[type] * 1000 * distanceWeightPair[msl.size] * (msl.IsBurning ? 1.0f : 0.5f);

            return dst <= weighedMaxDst;
        }

        // No, that's RWR
        //void Update()
        //{
        //    UpdateObjectsLifetime();
        //}
        //public void TransmittObjectData(TargetType type, GameObject transmitter, string displayText, bool isLocking, Vector3 position, bool needAlarm = true)
        //{
        //    if (!receivedObjects.Exists(x => x.signalTransmitter == transmitter))
        //    {
        //        ReceivedObjectData objectData = new ReceivedObjectData(type, transmitter, displayText, isLocking, position, defaultDisplayLifetime, needAlarm);
        //        receivedObjects.Add(objectData);

        //        if (objectData.isLockingUs && objectData.targetType == TargetType.MSL && !HUDDrawer.Instance.mslDetected.Exists(x => x.gameObject == objectData.signalTransmitter))
        //        {
        //            HUDDrawer.Instance.mslDetected.Add(objectData.signalTransmitter.GetComponent<Missile>());
        //        }
        //    }
        //    else
        //    {
        //        int index = receivedObjects.FindIndex(x => x.signalTransmitter == transmitter);
        //        ReceivedObjectData objData = receivedObjects[index];
        //        objData.targetType = type;
        //        objData.displayLifetime = defaultDisplayLifetime;
        //        objData.needAlarm = needAlarm;
        //        objData.isLockingUs = isLocking;
        //        objData.displayText = displayText;
        //        objData.position = position;
        //        receivedObjects[index] = objData;

        //        if (HUDDrawer.Instance.mslDetected.Exists(x => x.gameObject == objData.signalTransmitter))
        //        {
        //            int indexInDrawer = HUDDrawer.Instance.mslDetected.FindIndex(x => x.gameObject == objData.signalTransmitter);
        //            HUDDrawer.Instance.mslDetected[indexInDrawer] = objData.signalTransmitter.GetComponent<Missile>();
        //        }
        //        else if (objData.isLockingUs && objData.targetType == TargetType.MSL)
        //        {
        //            HUDDrawer.Instance.mslDetected.Add(objData.signalTransmitter.GetComponent<Missile>());
        //        }
        //    }
        //}

        //void UpdateObjectsLifetime()
        //{
        //    //update objects lifetime
        //    for (int i = receivedObjects.Count - 1; i >= 0; i--)
        //    {
        //        ReceivedObjectData objectData = receivedObjects[i];
        //        objectData.displayLifetime -= Time.deltaTime;
        //        if (objectData.needAlarm)
        //            objectData.needAlarm = false;

        //        receivedObjects[i] = objectData;

        //        if (receivedObjects[i].displayLifetime < 0)
        //        {
        //            receivedObjects.Remove(receivedObjects[i]);
        //            HUDDrawer.Instance.mslDetected.Remove(objectData.signalTransmitter.GetComponent<Missile>());
        //        }
        //    }
        //}
    }
}