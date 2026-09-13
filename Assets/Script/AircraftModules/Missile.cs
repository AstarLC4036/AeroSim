using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using AeroSim.Util;
using AeroSim.Utility;
using AeroSim.Utils;
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class Missile : MonoBehaviour
    {
        public enum MissileState
        {
            None,
            Locking,
            Locked,
            Memory
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
        [Header("Debug")]
        public bool disableNav = false;
        public bool noLostTrack = false;

        [Header("Base peroperties")]
        public string nameId = "Missile";
        public MissileSize size = MissileSize.Small;
        public MissileType type = MissileType.None;
        public bool hasDatalink = false;
        public LayerMask targetLayer;

        [Header("Flight")]
        public float guideGain;
        public float cmdAccelMultiply;
        public MissileFlightController flightController;
        public AeroSurface[] surfaces;
        public Vector3 centerOfMass;

        public Transform target;
        public FixedJoint connetor;

        // guiding logic
        [SerializeField]
        protected Vector3 controllingInput;
        protected Vector3 targetPos;
        protected Vector3 targetVelo;
        protected Vector3 lastPos;
        protected Vector3 targetDir;
        protected Vector3 prevVelo;
        protected float targetDst;
        protected Vector3 commandAccel;

        protected float velo;
        [SerializeField]
        protected Vector3 desiredDirection;
        protected Vector3 previousPosition;

        [Header("Performence")]
        public float accTime = 1f;
        public float accleration = 200;
        public float dragCoeff = 0.01f;
        public float maxTurnRate;
        public float maxTurnAcceleration;
        public float maxG;
        public float duration = 5f;
        public float burntTime = 0;
        public float thrust = 1000;
        [Header("Tracking")]
        public float maxRange = 13;
        public float lockTime = 0.5f;
        public float lockTimeout = 0.5f;
        public float datalinkTimeout = 0;
        public float boresightAngle = 30f;
        public float jitterAmplitude;
        public float jitterFreqency;
        [Header("Effect")]
        public EffectController flameEffect;
        public EffectController explosionEffect;

        [SerializeField]
        protected bool isIgnited = false;
        protected bool isLaunched = false;
        public Rigidbody rb;

        // lock logic
        protected float lockTimer;
        protected float lockingTimer;
        protected float dataLinkTimer;
        public MissileState lockState = MissileState.None;


        //public float a;
        //public float ac;
        //public float bPow;
        //public float k1;
        //public float k2;

        public Aircraft parentAircraft;
        public Aircraft targetAircraft;

        public bool IsBurning => isIgnited && burntTime < duration;
        public bool IsIgnited => isIgnited;
        public bool IsLaunched => isLaunched;
        public Vector3 TargetPosition => targetPos;

        // Use this for initialization
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            OriginKeeper.onOriginChange += OnOriginChange;
            flightController.missile = this;

            foreach (AeroSurface surface in surfaces)
            {
                surface.parent = transform;
            }
        }

        protected virtual void OnOriginChange(Vector3 delta)
        {
            lastPos += delta;
            previousPosition += delta;
            targetPos += delta;
        }

        void FixedUpdate()
        {
            UpdateState(Time.fixedDeltaTime);
            UpdateLock(Time.fixedDeltaTime);
            UpdateAeroForces();

            if (isIgnited)
            {
                UpdatePosition(Time.fixedDeltaTime);

                // tracking
                if (burntTime >= accTime)
                {
                    if(!disableNav)
                        UpdateTrack(Time.fixedDeltaTime);
                }

                // hit test
                UpdateHit();
            }

            UpdateTransmit();
            UpdateInput(Time.fixedDeltaTime);
        }

        public void UpdateAeroForces()
        {
            BiVector3 forcesAndTorque = new BiVector3();
            foreach (AeroSurface surface in surfaces)
            {
                Vector3 localCenterOfMass = centerOfMass.x * transform.forward + centerOfMass.y * transform.up + centerOfMass.z * transform.right;
                BiVector3 forces = surface.CalcucateForces(localCenterOfMass);
                forcesAndTorque += forces;
                Debug.DrawLine(surface.transform.position, surface.transform.position + surface.LocalVelocity / 10, Color.white);
                Debug.DrawLine(surface.transform.position, surface.transform.position + forces.lift / 100, Color.green);
                Debug.DrawLine(surface.transform.position, surface.transform.position + forces.drag / 100, Color.red);
                Debug.DrawLine(surface.transform.position, surface.transform.position + forces.torque / 100, Color.blue);
            }

            rb.AddForce(forcesAndTorque.lift);
            rb.AddForce(forcesAndTorque.drag);
            rb.AddTorque(forcesAndTorque.torque);

        }

        /// <summary>
        /// Set target position.
        /// </summary>
        /// <param name="position">Target position</param>
        public virtual void SendTargetData(Vector3 position)
        {

        }

        public void SetTarget(Transform target)
        {
            this.target = target;

            if(target != null)
                targetPos = target.position;
        }

        /// <summary>
        /// Update tracking state.
        /// </summary>
        /// <param name="dt">Delta time</param>
        protected virtual void UpdateState(float dt)
        {
            velo = rb.velocity.magnitude;

            if (target != null)
            {
                targetPos = target.position;

                targetVelo = (targetPos - lastPos) / Time.fixedDeltaTime;
                lastPos = targetPos;

                targetDir = MathUtility.ApplyAngularJitter((target.position - transform.position).normalized, jitterAmplitude, jitterFreqency, transform.up);
                targetDst = Vector3.Distance(transform.position, targetPos);

                if (targetAircraft == null)
                {
                    Component component;
                    target.gameObject.TryGetComponent(typeof(Aircraft), out component);
                    if (component != null)
                        targetAircraft = (Aircraft)component;
                }
            }
            else if(targetAircraft != null)
            {
                targetAircraft = null;
            }
        }

        protected virtual void UpdateInput(float dt)
        {
            float clampedX = Mathf.Clamp(controllingInput.x, -1, 1);
            float clampedY = Mathf.Clamp(controllingInput.y, -1, 1);
            float clampedZ = Mathf.Clamp(controllingInput.z, -1, 1);
            controllingInput = new Vector3(clampedX, clampedY, clampedZ);

            foreach (AeroSurface surface in surfaces)
            {
                surface.UpdateInput(controllingInput);
            }
        }

        /// <summary>
        ///  Update RWR transmittion
        /// </summary>
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

        /// <summary>
        /// Detect whether the missile hits target.
        /// </summary>
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
            previousPosition = transform.position;
        }

        protected virtual void UpdatePosition(float dt)
        {
            if (isIgnited)
            {
                //if (desiredDirection != Vector3.zero)
                //{
                //    float angleToTarget = Vector3.Angle(transform.forward, desiredDirection);

                //    // target angular velocity
                //    float desiredTurnRate = Mathf.Clamp(angleToTarget / dt, 0f, maxTurnRate);

                //    // angular velocity
                //    currentTurnRate = Mathf.MoveTowards(
                //        currentTurnRate, desiredTurnRate, maxTurnAcceleration * dt);

                //    // rotation
                //    Quaternion targetRotation = Quaternion.LookRotation(desiredDirection);
                //    transform.rotation = Quaternion.RotateTowards(
                //        transform.rotation, targetRotation, currentTurnRate * dt);
                //}

                if (burntTime < duration)
                {
                    //transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);
                    burntTime += Time.fixedDeltaTime;
                    rb.AddForce(transform.forward * thrust);
                    //velo += accleration * Time.fixedDeltaTime;
                }
                else if (burntTime >= duration)
                {
                    burntTime = duration;
                    //flameParticle.Stop();
                    flameEffect.Stop();
                }
            }

            // drag
            rb.AddForce(-rb.velocity.normalized * rb.velocity.sqrMagnitude * dragCoeff);
            //transform.position += transform.forward * velo * Time.fixedDeltaTime;
        }

        /// <summary>
        /// Update lock state.
        /// </summary>
        /// <param name="dt">Delta time</param>
        protected virtual void UpdateLock(float dt)
        {
            if(lockState == MissileState.Locking)
            {
                lockTimer -= dt;
                if(lockTimer < 0)
                {
                    lockTimer = 0;
                    lockState = MissileState.Locked;
                    lastPos = target.transform.position;
                }
            }
        }

        protected virtual void UpdateTrack(float dt)
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

            //Vector3 relativePosition = targetPos - transform.position; // 相对坐标
            float range = targetDst; // 距离
            Vector3 losDirection = targetDir; // 方向
            Vector3 relativeVelocity = targetVelo - rb.velocity; // 相对速度
            Vector3 losRate = Vector3.Cross(relativeVelocity, losDirection) / range; // 视线角速率
            //float closingVelocity = -Vector3.Dot(relativeVelocity, losDirection);
            commandAccel = guideGain * Vector3.Cross(rb.velocity, losRate); // 指令加速度
            commandAccel = Vector3.ClampMagnitude(commandAccel, maxG * 9.81f);
            Vector3 desiredVelo = rb.velocity + commandAccel * dt * cmdAccelMultiply; // 期望速度
            controllingInput = flightController.CalcInput(desiredVelo.normalized, dt);
            //Debug.Log($"input {controllingInput}, velocity {rb.velocity.magnitude}, command accel {commandAccel}");
        }

        public virtual void Ignite()
        {
            if(parentAircraft != null)
                velo = parentAircraft.Velocity.magnitude;

            AircraftManager.RegistMSL(this);
            isIgnited = true;
            flameEffect.Play();
            GameObject.Destroy(connetor);
            //rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;

            isLaunched = true;
        }

        public void Explode(RaycastHit hit)
        {
            transform.position = hit.point;

            isIgnited = false;
            explosionEffect.Play();
            flameEffect.Stop();
            rb.isKinematic = true;

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

        public virtual void ActiveSeeker()
        {
            lockState = MissileState.Locking;
            lockTimer = lockTime;
            lockingTimer = 0;
        }

        public virtual void ShutdownSeeker()
        {
            if (lockState != MissileState.None)
            {
                lockState = MissileState.None;
                lockTimer = lockTime;
                lockingTimer = 0;
            }
        }

        public void DirectLock(Transform target)
        {
            this.target = target;
            lastPos = target.transform.position;
            lockState = MissileState.Locked;

            Component component;
            target.gameObject.TryGetComponent(typeof(Aircraft), out component);
            if (component != null)
                targetAircraft = (Aircraft)component;
        }

        protected virtual void OnDrawGizmos()
        {
            //Gizmos.DrawLine(transform.position, transform.position + trackVelo.normalized);
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(target.position, target.position + targetVelo);
            }
            if (desiredDirection != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, transform.position + desiredDirection * 10);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + commandAccel * 10);
            Gizmos.DrawLine(transform.position, transform.position + rb.velocity * 10);
        }
    }
}