using AeroSim.AeroPhysics;
using AeroSim.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class ActiveMissile : Missile
    {
        public float radarStartupRange;
        public float radarFov;
        private int status = 0; // 0 Unlocked; 1 Datalink Guiding; 2 Radar Guiding
        public int Status => status;

        private void Awake()
        {
            type = MissileType.Active;
        }

        public override void ActiveSeeker()
        {
            base.ActiveSeeker();
        }

        protected override void UpdateState()
        {
            targetVelo = (targetPos - lastPos) / Time.fixedDeltaTime;
            lastPos = targetPos;
        }

        protected override void UpdateLock(float dt)
        {
            if (lockState == LockState.Locking && status == 0)
            {
                //if (MathUtility.ConeDetect(transform.position, transform.forward, targetPos, radarFov))
                //{
                //    if(lockingTimer != 0)
                //        lockingTimer = 0;

                //    lockTimer -= dt;
                //}
                //else
                //{
                //    lockingTimer += dt;
                //    if (lockingTimer > lockTimeout)
                //        lockState = LockState.None;

                //    if (lockTimer < lockTime)
                //        lockTimer = lockTime;
                //}

                lockTimer -= dt;

                if (lockTimer < 0)
                {
                    lockingTimer = 0;
                    lockTimer = 0;
                    lockState = LockState.Locked;
                    lastPos = target.transform.position;
                    status = 1;
                }
            }
            else if(lockState == LockState.Locked)
            {
                if(Vector3.Distance(transform.position, targetPos) <= radarStartupRange && status == 1)
                {
                    status = 2;
                }

                if(MathUtility.ConeDetect(transform.position, transform.forward, targetPos, radarFov) && status == 2)
                {
                    targetPos = target.transform.position;
                }
            }
        }

        public override void SendTargetData(Vector3 position)
        {
            targetPos = position;
        }

        protected override void UpdateTransmit()
        {
            if (status == 2 && targetAircraft != null && targetAircraft.rwr != null)
            {
                targetAircraft.rwr.TransmittObjectData(RWRModule.TargetType.MSL, gameObject, "MSL", true, transform.position);
            }
        }
    }
}
