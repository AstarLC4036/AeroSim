using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using AeroSim.Utils;
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class Missile : MonoBehaviour
    {
        public enum LockState
        {
            None,
            Locking,
            Locked
        }

        public enum MissileSize
        {
            Small,
            Medium,
            Large,
            AntiRadiation,
            //Cruise,
        }

        public enum MissileType
        {
            None,
            IR,
            SemiActive,
            Active
        }

        public Transform target;
        public FixedJoint connetor;

        protected Vector3 targetPos;
        protected Vector3 targetVelo;
        protected Vector3 lastPos;
        protected float velo;

        [Header("Base Properties")]
        //Vector3 trackVelo;
        public float accTime = 1f;
        public float accleration = 200;
        public float dragCoeff = 0.01f;
        public float duration = 5f;
        public float burntTime = 0;
        public float thrust = 1000;
        public float lockTime = 0.5f;
        public float lockTimeout = 0.5f;
        public MissileSize size = MissileSize.Small;
        public MissileType type = MissileType.None;
        public bool hasDatalink = false;
        public LayerMask targetLayer;
        public EffectController flameEffect;
        public EffectController explosionEffect;
        [SerializeField]
        protected bool isIgnited = false;
        [SerializeField]
        protected Rigidbody rb;

        protected Vector3 previousPosition;
        protected float lockTimer;
        protected float lockingTimer;
        public LockState lockState = LockState.None;

        public Vector3 Velocity => velo * transform.forward;

        //public float a;
        //public float ac;
        //public float bPow;
        //public float k1;
        //public float k2;

        public Aircraft parentAircraft;
        public Aircraft targetAircraft;

        public bool IsBurning => isIgnited && burntTime < duration;
        public bool IsIgnited => isIgnited;
        public Vector3 TargetPosition => targetPos;

        Vector3 desiredDirection;

        // Use this for initialization
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            OriginKeeper.onOriginChange += OnOriginChange;
        }

        void OnOriginChange(Vector3 delta)
        {
            lastPos += delta;
            previousPosition += delta;
        }

        void FixedUpdate()
        {
            UpdateState();
            UpdateLock(Time.fixedDeltaTime);

            if (isIgnited)
            {
                UpdatePosition();

                // tracking
                if (burntTime >= accTime)
                    UpdateTrack();

                // hit test
                UpdateHit();
            }

            UpdateTransmit();
        }

        public virtual void SendTargetData(Vector3 position)
        {

        }

        protected virtual void UpdateState()
        {
            if (target != null)
            {
                targetPos = target.position;

                targetVelo = (targetPos - lastPos) / Time.fixedDeltaTime;
                lastPos = targetPos;
            }
        }

        protected virtual void UpdateTransmit()
        {
            if (isIgnited)
            {
                //TODO: make it more hardcore
                if (type == MissileType.Active && targetAircraft != null && targetAircraft.rwr != null)
                {
                    targetAircraft.rwr.TransmittObjectData(RWRModule.TargetType.MSL, gameObject, "MSL", true, transform.position);
                }
            }
        }

        protected virtual void UpdateHit()
        {
            RaycastHit hitInfo;
            if (Physics.SphereCast(previousPosition, 0.3f, (transform.position - previousPosition).normalized, out hitInfo, 100, targetLayer))
            {
                if (parentAircraft != null)
                {
                    if (!hitInfo.collider.transform.IsChildOf(parentAircraft.transform))
                        Explode(hitInfo);
                }
                else
                {
                    Explode(hitInfo);
                }
            }
        }

        protected virtual void UpdatePosition()
        {
            if (isIgnited && burntTime < duration)
            {
                burntTime += Time.fixedDeltaTime;
                //rb.AddForce(transform.forward * thrust * Time.fixedDeltaTime);
                velo += accleration * Time.fixedDeltaTime;
            }
            else if (burntTime > duration)
            {
                burntTime = duration;
                //flameParticle.Stop();
                flameEffect.Stop();
            }

            velo -= velo * dragCoeff;
            if (Mathf.Abs(velo) < 0.1f)
            {
                velo = 0;
            }

            previousPosition = transform.position;
            transform.position += transform.forward * velo * Time.fixedDeltaTime;
        }

        protected virtual void UpdateLock(float dt)
        {
            if(lockState == LockState.Locking)
            {
                lockTimer -= dt;
                if(lockTimer < 0)
                {
                    lockTimer = 0;
                    lockState = LockState.Locked;
                    lastPos = target.transform.position;
                }
            }
        }

        protected virtual void UpdateTrack()
        {
            // bad method
            /*
            //targetVelo = (target.position - lastPos) / Time.fixedDeltaTime;
            //lastPos = target.position;
            //Vector3 targetDir = (target.position - transform.position).normalized;

            //a = targetDir.x * targetDir.x + targetDir.y * targetDir.y + targetDir.z * targetDir.z;
            //bPow = Mathf.Pow(2 * targetDir.x * targetVelo.x + 2 * targetDir.y * targetVelo.y + 2 * targetDir.z * targetVelo.z, 2);
            //ac = a * (targetVelo.x * targetVelo.x + targetVelo.y * targetVelo.y + targetVelo.z * targetVelo.z - velo * velo);
            //k1 = (bPow - 4 * ac) / (2 * a);
            //k2 = (bPow + 4 * ac) / (2 * a);

            //if(Mathf.Max(k1, k2) < 0)
            //{
            //    return;
            //}

            //trackVelo = targetVelo + Mathf.Max(k1, k2) * targetDir;

            //transform.LookAt(transform.position + trackVelo.normalized);
            */

            Vector3 relativePosition = targetPos - transform.position;
            float range = relativePosition.magnitude;
            Vector3 losDirection = relativePosition.normalized;
            Vector3 relativeVelocity = targetVelo - velo * transform.forward;
            Vector3 losRate = Vector3.Cross(relativeVelocity, losDirection) / range;
            //float closingVelocity = -Vector3.Dot(relativeVelocity, losDirection);

            Vector3 commandAccel = 4 * Vector3.Cross(velo * transform.forward, losRate);
            //Vector3 localAccel = transform.InverseTransformDirection(commandAccel);
            Vector3 desiredVelo = transform.forward * velo + commandAccel * Time.fixedDeltaTime;
            desiredDirection = desiredVelo.normalized * 10;
            transform.LookAt(transform.position + desiredDirection);
        }

        public void Ignite()
        {
            if(parentAircraft != null)
                velo = parentAircraft.Velocity.magnitude;

            AircraftManager.RegistMSL(this);
            isIgnited = true;
            flameEffect.Play();
            GameObject.Destroy(connetor);
            rb.isKinematic = true;
        }

        public void Explode(RaycastHit hit)
        {
            transform.position = hit.point;

            velo = 0;
            isIgnited = false;
            explosionEffect.Play();
            flameEffect.Stop();

            float explosionRadius = 50;
            Collider[] targets = Physics.OverlapSphere(hit.point, explosionRadius);
            if (targets.Length > 0)
            {
                foreach (Collider target in targets)
                {
                    Rigidbody attachedRb = target.attachedRigidbody;
                    if (attachedRb != null)
                        attachedRb.AddExplosionForce(1E+7f, hit.point, explosionRadius);
                }
            }

            if(hasDatalink && parentAircraft != null && parentAircraft.datalink != null)
            {
                parentAircraft.datalink.UnregisterDatalink(this);
            }
        }

        public void Lock(Transform target)
        {
            this.target = target;
            lockState = LockState.Locking;
            lockTimer = lockTime;

            targetPos = target.position;

            if (hasDatalink && parentAircraft != null && parentAircraft.datalink != null)
            {
                parentAircraft.datalink.RegisterDatalink(this);
            }

            Component component;
            target.gameObject.TryGetComponent(typeof(Aircraft), out component);
            if (component != null)
                targetAircraft = (Aircraft)component;
        }

        public void DirectLock(Transform target)
        {
            this.target = target;
            lastPos = target.transform.position;
            lockState = LockState.Locked;

            if (hasDatalink && parentAircraft != null && parentAircraft.datalink != null)
            {
                parentAircraft.datalink.RegisterDatalink(this);
            }

            Component component;
            target.gameObject.TryGetComponent(typeof(Aircraft), out component);
            if (component != null)
                targetAircraft = (Aircraft)component;
        }

        public void OnDrawGizmos()
        {
            //Gizmos.DrawLine(transform.position, transform.position + trackVelo.normalized);
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(target.position, target.position + targetVelo);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, transform.position + desiredDirection);
            }
        }
    }
}