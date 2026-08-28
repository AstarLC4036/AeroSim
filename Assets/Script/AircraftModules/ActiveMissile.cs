using AeroSim.AeroPhysics;
using AeroSim.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
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

        protected override void UpdateState(float dt)
        {
            // Update target position and velocity
            if (lockState == MissileState.Locked)
            {
                if (status == 2 && MathUtility.ConeDetect(transform.position, transform.forward, targetPos, radarFov))
                {
                    targetPos = target.position;
                }

                targetVelo = (targetPos - lastPos) / dt;
                lastPos = targetPos;
            }
            else if(lockState == MissileState.Memory)
            {
                targetPos += prevVelo * dt;
                lastPos = targetPos;
            }

            // Update tracking data
            Vector3 relativeTargetPosition = targetPos - transform.position;
            targetDir = relativeTargetPosition.normalized;
            targetDst = relativeTargetPosition.magnitude;
        }

        protected override void UpdateLock(float dt)
        {
            if (lockState == MissileState.Locking && status == 0)
            {
                // Detect whether the target in the aircraft radar search range
                if (parentAircraft.radar != null && parentAircraft.radar.TargetDetect(target.position))
                {
                    // count down and lock
                    lockTimer -= dt;

                    if (lockTimer < 0)
                    {
                        lockingTimer = 0;
                        lockTimer = 0;
                        lockState = MissileState.Locked;
                        lastPos = target.transform.position;
                        status = 1;
                        dataLinkTimer = 0;
                    }
                }
                // if not, reset the lock timer
                else if(lockingTimer != lockTime)
                {
                    lockTimer = lockTime;
                }
            }
            else if (lockState == MissileState.Locked)
            {
                if(status == 1)
                {
                    // if datalink didn't transmit the target data(lost lock/track), switch to memory mode
                    if (dataLinkTimer < datalinkTimeout)
                        dataLinkTimer += dt;
                    else
                    {
                        if (isLaunched)
                        {
                            lockState = MissileState.Memory;
                        }
                        else
                        {
                            lockState = MissileState.Locking;
                            status = 0;
                            lockTimer = lockTime;
                            lockingTimer = 0;
                        }
                    }

                    // If it's close enough, active the radar seeker
                    if (Vector3.Distance(transform.position, targetPos) <= radarStartupRange)
                    {
                        status = 2;
                    }
                }

                // lost lock => switch to memory mode
                if (status == 2)
                {
                    if (!MathUtility.ConeDetect(transform.position, transform.forward, targetPos, radarFov))
                    {
                        if (isLaunched)
                        {
                            lockState = MissileState.Memory;
                        }
                        else
                        {
                            lockState = MissileState.Locking;
                            status = 0;
                            lockTimer = lockTime;
                            lockingTimer = 0;
                        }
                    }
                }
            }
            else if (lockState == MissileState.Memory)
            {
                // if we lock the target again, switch back to 'Locked' mode
                if (MathUtility.ConeDetect(transform.position, transform.forward, targetPos, radarFov))
                {
                    lockState = MissileState.Locked;
                }
            }
        }

        public override void SendTargetData(Vector3 position)
        {
            targetPos = position;
            dataLinkTimer = 0;
            prevVelo = targetVelo;

            if (lockState == MissileState.Memory)
            {
                lockState = MissileState.Locked;
            }

            if(dataLinkTimer != 0)
            {
                dataLinkTimer = 0;
            }
        }

        protected override void UpdateTransmit()
        {
            if (status == 2 && targetAircraft != null && targetAircraft.rwr != null)
            {
                targetAircraft.rwr.TransmittObjectData(RWRModule.TargetType.MSL, gameObject, "MSL", true, transform.position);
            }
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (lockState != MissileState.None)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(targetPos, 10);

                Gizmos.color = Color.white;
                Handles.DrawWireDisc(transform.position + transform.forward * 10, transform.forward, 10 * Mathf.Tan(radarFov * Mathf.Deg2Rad));
                Gizmos.DrawRay(transform.position, (transform.forward + transform.up * Mathf.Tan(radarFov * Mathf.Deg2Rad)) * 100);
                Gizmos.DrawRay(transform.position, (transform.forward - transform.up * Mathf.Tan(radarFov * Mathf.Deg2Rad)) * 100);
                Gizmos.DrawRay(transform.position, (transform.forward + transform.right * Mathf.Tan(radarFov * Mathf.Deg2Rad)) * 100);
                Gizmos.DrawRay(transform.position, (transform.forward - transform.right * Mathf.Tan(radarFov * Mathf.Deg2Rad)) * 100);
            }
        }
    }
}
