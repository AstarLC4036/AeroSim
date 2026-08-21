using AeroSim.AeroPhysics;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class DatalinkModule : AircraftModule
    {
        public List<Missile> missiles = new List<Missile>();

        public void FixedUpdate()
        {
            UpdateMSLData();
        }

        private void UpdateMSLData()
        {
            foreach(Missile missile in missiles)
            {
                if(missile.type == Missile.MissileType.Active)
                {
                    ActiveMissile activeMsl = missile as ActiveMissile;
                    if(activeMsl.Status == 1 && activeMsl.hasDatalink && parentAircraft.radar != null && missile.targetAircraft != null && parentAircraft.radar.ScannedAircrafts.Exists(x => x == activeMsl.targetAircraft))
                    {
                        activeMsl.SendTargetData(activeMsl.targetAircraft.transform.position); // For the simulation
                    }
                }
            }
        }

        public void RegisterDatalink(Missile missile)
        {
            if(!missiles.Exists(x => x == missile))
                missiles.Add(missile);
        }

        public void UnregisterDatalink(Missile missile)
        {
            missiles.Remove(missile);
        }
    }
}