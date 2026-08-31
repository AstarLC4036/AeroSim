using AeroSim.AeroPhysics;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class DatalinkModule : AircraftModule
    {
        public List<(Missile, Transform)> mslTrackInfo = new List<(Missile, Transform)>();

        public void FixedUpdate()
        {
            UpdateMSLData();
        }

        private void UpdateMSLData()
        {
            foreach((Missile, Transform) missileAndTarget in mslTrackInfo)
            {
                Missile missile = missileAndTarget.Item1;
                Transform target = missileAndTarget.Item2;
                if(missile.type == Missile.MissileType.Active)
                {
                    ActiveMissile activeMsl = missile as ActiveMissile;
                    if(activeMsl.Status == 1 && activeMsl.hasDatalink 
                       && parentAircraft.radar != null && target != null && parentAircraft.radar.TargetDetect(target.position))
                    {
                        activeMsl.SendTargetData(target.transform.position); // For the simulation
                    }
                }
            }
        }

        public void RegisterDatalink(Missile missile, Transform target)
        {
            if(!mslTrackInfo.Exists(x => x.Item1 == missile))
                mslTrackInfo.Add((missile, target));
        }

        public void UnregisterDatalink(Missile missile)
        {
            mslTrackInfo.Remove(mslTrackInfo.Find(x => x.Item1 == missile));
        }
    }
}