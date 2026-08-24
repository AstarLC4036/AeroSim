using AeroSim.AeroPhysics;
using AeroSim.Audio;
using AeroSim.General;
using AeroSim.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static Unity.VisualScripting.Member;

namespace AeroSim.AircraftModules
{
    public class IRMissile : Missile
    {
        public Transform seekerTransform;
        public Vector2 yawLimits;
        public Vector2 pitchLimits;
        public float seekerFov;
        public float prewarmTime;
        private float prewarmTimer;
        public Vector3 seekerDirection;
        public bool isPrewarmCompleted;

        [SerializeField]
        private List<IRSource> irList;

        private void Awake()
        {
            type = MissileType.IR;
        }

        public override void ActiveSeeker()
        {
            base.ActiveSeeker();
            irList = new List<IRSource>();
            seekerDirection = seekerTransform.forward;
            prewarmTimer = 0;
        }

        protected override void UpdateState()
        {
            if (lockState == LockState.Locking)
            {
                seekerDirection = seekerTransform.forward;
            }
            else if (target != null && lockState == LockState.Locked)
            {
                if (MathUtility.ConeDetect(seekerTransform.position, seekerDirection, target.position, seekerFov))
                {
                    targetPos = target.position;
                    seekerDirection = (targetPos - seekerTransform.position).normalized;
                    seekerDirection = MathUtility.ClampTargetDir(transform, seekerDirection, yawLimits.x, yawLimits.y, pitchLimits.x, pitchLimits.y);
                }
            }
        }

        protected override void UpdateLock(float dt)
        {
            if (lockState == LockState.Locking)
            {
                if (prewarmTimer < prewarmTime && !isPrewarmCompleted)
                {
                    prewarmTimer += dt;
                    return;
                }
                if(prewarmTimer >= prewarmTime && !isPrewarmCompleted)
                {
                    isPrewarmCompleted = true;
                    if(!IsLaunched && parentAircraft.isControlling)
                        AudioManager.MissileIRSearch();
                }

                // Find all IR Source in search range
                foreach (var irSrc in AircraftManager.IRSources)
                {
                    float dst = Vector3.Distance(seekerTransform.position, irSrc.transform.position);
                    if (dst < maxRange * 1000 && MathUtility.ConeDetect(seekerTransform.position, seekerDirection, irSrc.transform.position, seekerFov))
                    {
                        if (!irList.Exists(x => x == irSrc))
                        {
                            irList.Add(irSrc);
                        }
                    }
                    else if(irList.Exists(x => x == irSrc))
                    {
                        irList.Remove(irSrc);
                    }
                }

                // Find the best source
                if (irList.Count > 0)
                {
                    if (lockingTimer != 0)
                        lockingTimer = 0;

                    float minWeight = float.MaxValue;
                    IRSource tempTarget = null;
                    foreach (var irSrc in irList)
                    {
                        float totalWeight = irSrc.intensity * 1f + 1 / Mathf.Max(Vector3.Angle(seekerTransform.forward, irSrc.transform.position - seekerTransform.position), 0.001f) * 1f;
                        if (totalWeight < minWeight)
                        {
                            tempTarget = irSrc;
                            minWeight = totalWeight;
                        }
                    }

                    // Update locking timer
                    if (tempTarget != null && target != tempTarget.transform)
                    {
                        float intensityFactor = Mathf.Clamp01(tempTarget.intensity / 5f);   // range 0 ~ 5
                        float intensityMultiplier = Mathf.Lerp(1.5f, 0.5f, intensityFactor);

                        float distanceFactor = Mathf.Clamp01(Vector3.Distance(transform.position, tempTarget.transform.position) / maxRange); // 0 ~ 1
                        float distanceMultiplier = Mathf.Lerp(0.4f, 1.0f, distanceFactor);

                        SetTarget(tempTarget.transform);
                        lockTimer = lockTime * Mathf.Clamp01(intensityMultiplier * distanceMultiplier);
                        lockingTimer = 0;
                    }

                    if(target != null && !MathUtility.ConeDetect(seekerTransform.position, seekerDirection, target.position, seekerFov))
                    {
                        SetTarget(null);
                    }

                    if(target != null)
                        lockTimer -= dt;
                    else
                        lockingTimer += dt;

                    if (lockTimer < 0)
                    {
                        lockingTimer = 0;
                        lockTimer = lockTime;
                        lockState = LockState.Locked;
                        lastPos = target.transform.position;

                        if(!IsLaunched && parentAircraft.isControlling)
                            AudioManager.MissileIRLock();
                    }

                    if (lockingTimer > lockTimeout)
                    {
                        lockState = LockState.None;
                        target = null;
                        lockingTimer = 0;
                        lockTimer = lockTime;
                    }
                }
            }
            else if (target != null && lockState == LockState.Locked && !IsLaunched)
            {
                if (!MathUtility.ConeDetect(seekerTransform.position, seekerDirection, target.position, seekerFov))
                {
                    lockState = LockState.Locking;
                    lockingTimer = 0;
                    lockTimer = lockTime;
                    target = null;
                    seekerTransform.localRotation = Quaternion.identity;
                    seekerDirection = seekerTransform.forward;
                    if (parentAircraft.isControlling)
                        AudioManager.MissileIRSearch();
                }
            }
        }

        public override void Ignite()
        {
            base.Ignite();
            AudioManager.MissileStop();
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = Color.green;
            Gizmos.DrawLine(seekerTransform.position, seekerTransform.position + seekerDirection * 10);
            Gizmos.color = Color.white;
            //Handles.DrawWireDisc(transform.position, transform.forward, 0);
            // draw search cone
            Handles.DrawWireDisc(seekerTransform.position + seekerTransform.forward * 10, seekerTransform.forward, 10 * Mathf.Tan(seekerFov * Mathf.Deg2Rad));
            Gizmos.DrawRay(seekerTransform.position, (seekerTransform.forward + seekerTransform.up * Mathf.Tan(seekerFov * Mathf.Deg2Rad)) * 100);
            Gizmos.DrawRay(seekerTransform.position, (seekerTransform.forward - seekerTransform.up * Mathf.Tan(seekerFov * Mathf.Deg2Rad)) * 100);
            Gizmos.DrawRay(seekerTransform.position, (seekerTransform.forward + seekerTransform.right * Mathf.Tan(seekerFov * Mathf.Deg2Rad)) * 100);
            Gizmos.DrawRay(seekerTransform.position, (seekerTransform.forward - seekerTransform.right * Mathf.Tan(seekerFov * Mathf.Deg2Rad)) * 100);
        }
    }
}
