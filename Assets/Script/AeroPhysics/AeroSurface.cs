using AeroSim.InputSystem;
using AeroSim.Util;
using AeroSim.Utils;
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using static AeroSim.AeroPhysics.AeroSurface;

namespace AeroSim.AeroPhysics
{
    //References : 
    //(ResearchGate)Real-time modeling of agile fixed-wing UAV aerodynamics
    //(GitHub: gasgiant)Aircraft-Physics
    public class AeroSurface : MonoBehaviour
    {
        public enum ControlSurfaceType
        {
            None = 0,
            Yaw = 1,
            Pitch = 2,
            Roll = 3
        }
        [Header("Status")]
        private Vector3 velocity;
        private Vector3 localFlow;
        private float angleOfAttack;
        private Vector3 lastPos;

        public Vector3 LocalVelocity => velocity;

        [Header("Data")]
        //public AeroSurfaceData surfaceData;
        public Aircraft parent;
        #region Surface Data
        public float span = 0;
        public float chord = 0;
        public float flapPercent = 0;
        public float liftSlope = 1;
        public float zeroLiftAngle = 0;
        public float aoaStallNB = -30; //Negative stall angle of attack(Base)
        public float aoaStallPB = 30; //Positive stall angle of attack(Base)
        public float skinFrictionCoiffient = 1;
        public float aspectRatio = 0;
        public bool autoAR = true;

        public float area
        {
            get
            {
                return span * chord;
            }
        }

        public float UpdateAspectRatio()
        {
            aspectRatio = (span * span) / (span * chord);
            return aspectRatio;
        }
        #endregion

        [Header("Physics")]
        public Vector3 coefficientsInfo;

        [Header("Effect")]
        public ParticleSystem wingTipVotexEmitter;

        [Header("Surface Settings")]
        public float flapAngle = 0;
        public float maxFlapAngle = 0;
        public float flapMoveSpeed = 0;
        public bool inverseTorque = false;
        public ControlSurfaceSettings[] controlSurfaces;
        [SerializeField]
        private float targetFlapAngle;

        public Transform flapTransform;
        public Axis rotateAxis;

        private Mesh surfaceMesh;
        private Mesh flapMesh;

        public float TargetFlapAngle
        {
            get
            {
                return targetFlapAngle;
            }
        }

        private void Awake()
        {
            OriginKeeper.onOriginChange += OnOriginChanged;
        }

        private void Start()
        {
            if (gameObject.activeSelf && enabled)
                UpdateData();

            lastPos = transform.position;
        }

        private void OnValidate()
        {
            if (gameObject.activeSelf && enabled)
                UpdateData();
        }

        private void OnOriginChanged(Vector3 delta)
        {
            lastPos += delta;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawMesh(surfaceMesh, 0, transform.position, transform.rotation);
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.DrawMesh(flapMesh, 0, transform.position, transform.rotation);
        }

        public void FixedUpdate()
        {
            CalcucateState();
        }

        public void Update()
        {
            //UpdateInput();
            UpdateControlSurface();
        }

        public void CalcucateState()
        {
            velocity = (transform.position - lastPos) / Time.deltaTime;
            localFlow = transform.InverseTransformVector(velocity);
            angleOfAttack = Mathf.Atan2(-localFlow.y, localFlow.z) * Mathf.Rad2Deg;

            lastPos = transform.position;
        }

        public void UpdateInput(Vector3 input)
        {
            if (controlSurfaces.Length == 0)
                return;

            float angle = 0;
            foreach (ControlSurfaceSettings surface in controlSurfaces)
            {
                angle += surface.GetAngle(input) * maxFlapAngle;
            }
            angle /= controlSurfaces.Length; //avg
            targetFlapAngle = angle;
        }

        private void UpdateParticles(LDMCoefficients coefficients)
        {
            if (wingTipVotexEmitter != null && coefficients.liftCoefficient >= 0.2f && !wingTipVotexEmitter.isPlaying)
            {
                wingTipVotexEmitter.Play();
            }
            else if (wingTipVotexEmitter != null && coefficients.liftCoefficient < 0.2f && wingTipVotexEmitter.isPlaying)
            {
                wingTipVotexEmitter.Stop();
            }

            if (wingTipVotexEmitter != null && wingTipVotexEmitter.isPlaying)
            {
                ParticleSystem.MainModule main = wingTipVotexEmitter.main;
                main.startSpeed = velocity.magnitude;
            }
        }

        public void UpdateData()
        {
            //if(surfaceData == null)
            //{
            //    return;
            //}

            if(autoAR)
                UpdateAspectRatio();

            float chordSurface = chord * (1 - flapPercent);
            float chordFlap = chord * flapPercent;
            GenerateSurfaceMesh(new Vector3(span, 0, chordSurface), chordFlap / 2, ref surfaceMesh);
            GenerateSurfaceMesh(new Vector3(span, 0, chordFlap), chordSurface / -2, ref flapMesh);

            /*
            sizeFixed.x = surfaceData.span;
            sizeFixed.z = surfaceData.chord * (1 - surfaceData.flapPercent);

            surfaceMesh = new Mesh();
            Vector3[] vertices = new Vector3[] {
                new Vector3(-sizeFixed.x / 2, sizeFixed.y / 2, sizeFixed.z / 2),
                new Vector3(sizeFixed.x / 2,  sizeFixed.y / 2, sizeFixed.z / 2),
                new Vector3(-sizeFixed.x / 2, sizeFixed.y / 2, -sizeFixed.z / 2),
                new Vector3(sizeFixed.x / 2,  sizeFixed.y / 2, -sizeFixed.z / 2),
                new Vector3(-sizeFixed.x / 2, -sizeFixed.y / 2, sizeFixed.z / 2),
                new Vector3(sizeFixed.x / 2,  -sizeFixed.y / 2, sizeFixed.z / 2),
                new Vector3(-sizeFixed.x / 2, -sizeFixed.y / 2, -sizeFixed.z / 2),
                new Vector3(sizeFixed.x / 2,  -sizeFixed.y / 2, -sizeFixed.z / 2)
            };
            //int[] triangles = new int[] { 0, 1, 2, 1, 3, 2, 0, 2, 6, 0, 6, 4, 0, 5, 1, 0, 4, 5, 1, 7, 3, 1, 5, 7, 2, 3, 7, 2, 7, 6, 4, 6, 5, 5, 6, 7 };
            int[] triangles = new int[] { 0, 1, 2, 1, 3, 2 };
            surfaceMesh.vertices = vertices;
            surfaceMesh.triangles = triangles;
            surfaceMesh.RecalculateNormals();
            */
        }

        public void GenerateSurfaceMesh(Vector3 size, float offset, ref Mesh mesh)
        {
            mesh = new Mesh();
            Vector3[] vertices = new Vector3[] {
                new Vector3(-size.x / 2, size.y / 2, size.z / 2 + offset),
                new Vector3(size.x / 2,  size.y / 2, size.z / 2 + offset),
                new Vector3(-size.x / 2, size.y / 2, -size.z / 2 + offset),
                new Vector3(size.x / 2,  size.y / 2, -size.z / 2 + offset),
                new Vector3(-size.x / 2, -size.y / 2, size.z / 2 + offset),
                new Vector3(size.x / 2,  -size.y / 2, size.z / 2 + offset),
                new Vector3(-size.x / 2, -size.y / 2, -size.z / 2 + offset),
                new Vector3(size.x / 2,  -size.y / 2, -size.z / 2 + offset)
            };
            //int[] triangles = new int[] { 0, 1, 2, 1, 3, 2, 0, 2, 6, 0, 6, 4, 0, 5, 1, 0, 4, 5, 1, 7, 3, 1, 5, 7, 2, 3, 7, 2, 7, 6, 4, 6, 5, 5, 6, 7 };
            int[] triangles = new int[] { 0, 1, 2, 1, 3, 2 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }

        public BiVector3 CalcucateForces(Vector3 aircraftCenterOfMass)
        {
            return CalcucateForces(angleOfAttack, velocity, aircraftCenterOfMass);
        }

        public BiVector3 CalcucateForces(float angleOfAttack, Vector3 velocity, Vector3 aircraftCenterOfMass)
        {
            LDMCoefficients coefficients = CalcucateCoiffients(angleOfAttack);

            float dymaticPresure = 0.5f * 1.225f * velocity.sqrMagnitude;

            float liftForce = dymaticPresure * coefficients.liftCoefficient * area;
            Vector3 lift = liftForce * transform.up;
            //Vector3 lift = liftForce * Vector3.Cross(-velocity.normalized, -rightAxis);

            if(coefficients.dragCoefficient < 0.01f)
            {
                coefficients.dragCoefficient = 0.01f;
            }

            float dragForce = dymaticPresure * coefficients.dragCoefficient * area;
            Vector3 drag = dragForce * -velocity.normalized;

            Vector3 torque = transform.right * coefficients.torqueCoefficient * dymaticPresure * area * chord;
            Vector3 totalTorque = Vector3.Cross(transform.position - parent.transform.position - aircraftCenterOfMass, lift + drag);
            if (inverseTorque)
                totalTorque *= -1;

            //actually, it shouldn't be here
            UpdateParticles(coefficients);

            return new BiVector3(lift, drag, totalTorque);
        }

        public Vector3 CalcucateLift(Vector3 velocity, float LC)
        {
            float dymaticPresure = 0.5f * 1.225f * velocity.sqrMagnitude;
            float liftForce = dymaticPresure * LC * area;
            Vector3 liftDir = Vector3.Cross(transform.right, velocity).normalized;
            Vector3 lift = liftForce * liftDir;
            return lift;
        }

        public Vector3 CalcucateDrag(Vector3 velocity, float DC)
        {
            float dymaticPresure = 0.5f * 1.225f * velocity.sqrMagnitude;
            float dragForce = dymaticPresure * DC * area;
            Vector3 drag = dragForce * -velocity.normalized;
            return drag;
        }

        public BiVector3 CalcucateForces(Vector3 velocity, float LC, float DC)
        {
            Vector3 lift = CalcucateLift(velocity, LC);
            Vector3 drag = CalcucateDrag(velocity, DC);

            return new BiVector3(lift, drag);
        }

        public Vector3 CalcucateTorque(Vector3 velocity, Vector3 totalForce, Vector3 worldCenterOfMass, float TC)
        {
            float dymaticPresure = 0.5f * 1.225f * velocity.sqrMagnitude;
            Vector3 torque = transform.right * TC * dymaticPresure * area * chord;
            return Vector3.Cross(transform.position - worldCenterOfMass, totalForce) + torque;
        }

        public void UpdateControlSurface()
        {
            float flapAngleDeg = flapAngle * Mathf.Rad2Deg;

            if (flapAngleDeg < targetFlapAngle)
            {
                flapAngleDeg += flapMoveSpeed * Time.fixedDeltaTime;
                if(flapAngleDeg > targetFlapAngle)
                {
                    flapAngleDeg = targetFlapAngle;
                }
            }
            else if(flapAngleDeg > targetFlapAngle)
            {
                flapAngleDeg -= flapMoveSpeed * Time.fixedDeltaTime;
                if (flapAngleDeg < targetFlapAngle)
                {
                    flapAngleDeg = targetFlapAngle;
                }
            }

            flapAngle = flapAngleDeg * Mathf.Deg2Rad;

            if (flapTransform != null)
            {
                switch (rotateAxis)
                {
                    case Axis.X:
                        flapTransform.localRotation = Quaternion.Euler(flapAngleDeg, flapTransform.localEulerAngles.y, flapTransform.localEulerAngles.z);
                        break;
                    case Axis.Y:
                        flapTransform.localRotation = Quaternion.Euler(flapTransform.localEulerAngles.x, flapAngleDeg, flapTransform.localEulerAngles.z);
                        break;
                    case Axis.Z:
                        flapTransform.localRotation = Quaternion.Euler(flapTransform.localEulerAngles.x, flapTransform.localEulerAngles.y, flapAngleDeg);
                        break;
                }

            }
        }

        //Real-time Aerodynamics Modeling
        public LDMCoefficients CalcucateCoiffients(float aoa)
        {
            LDMCoefficients coefficients;

            float aoaRad = aoa * Mathf.Deg2Rad;

            float correctedLiftSlope = liftSlope * (aspectRatio / (aspectRatio + 2 * (aspectRatio + 4) / (aspectRatio + 2)));//CLa
            float flapFraction = flapPercent; //Equals surfaceData.chord * surfaceData.flapPercent(flap chord) / surfaceData.chord
            float theta = Mathf.Acos(2 * flapFraction - 1);
            float deltaCL = correctedLiftSlope * (1 - (theta - Mathf.Sin(theta)) / Mathf.PI) * Mathf.Lerp(0.8f, 0.4f, (Mathf.Abs(flapAngle) * Mathf.Rad2Deg - 10) / 50) * flapAngle;
            float deltaCLMax = deltaCL * Mathf.Clamp01(1 - 0.5f * (flapFraction - 0.1f) / 0.3f);
            float CLMaxP = correctedLiftSlope * (aoaStallPB * Mathf.Deg2Rad - zeroLiftAngle * Mathf.Deg2Rad) + deltaCLMax; // P means positive
            float CLMaxN = correctedLiftSlope * (aoaStallNB * Mathf.Deg2Rad - zeroLiftAngle * Mathf.Deg2Rad) + deltaCLMax; // N means negative
            float aoaZero = zeroLiftAngle * Mathf.Deg2Rad - deltaCL / correctedLiftSlope; 
            float aoaStallP = aoaZero + CLMaxP / correctedLiftSlope;
            float aoaStallN = aoaZero + CLMaxN / correctedLiftSlope;

            float paddingAngleHigh = Mathf.Deg2Rad * Mathf.Lerp(15, 5, (Mathf.Rad2Deg * flapAngle + 50) / 100);
            float paddingAngleLow = Mathf.Deg2Rad * Mathf.Lerp(15, 5, (-Mathf.Rad2Deg * flapAngle + 50) / 100);
            float paddedStallAngleHigh = aoaStallP + paddingAngleHigh;
            float paddedStallAngleLow = aoaStallN - paddingAngleLow;

            //Low Angle of Attack Aerodynamics 
            if (aoaStallN < aoaRad && aoaRad < aoaStallP) // aStall,N < a < aStall,P
            {
                LDMCoefficients coef = CalculateCoefficientsAtLowAOA(aoaRad, aoaZero, correctedLiftSlope, aspectRatio);

                coefficientsInfo = coef.ToVector3();
                coefficients = coef;
            }

            //High Angle of Attack Aerodynamics 
            else
            {
                if (aoaRad > paddedStallAngleHigh || aoaRad < paddedStallAngleLow) //aoa < aStall,N or aStall,P < aoa
                {
                    //Rewrited in a func.

                    LDMCoefficients coef = CalculateCoefficientsAtHighAOA(aoaRad, aoaStallP, aoaStallN, aoaZero, correctedLiftSlope, aspectRatio);

                    coefficientsInfo = coef.ToVector3();
                    coefficients = coef;
                }
                else
                {
                    // Linear stitching in-between stall and low angles of attack modes.
                    Vector3 aerodynamicCoefficientsLow;
                    Vector3 aerodynamicCoefficientsStall;
                    float lerpParam;

                    if (aoaRad > aoaStallP)
                    {
                        aerodynamicCoefficientsLow = CalculateCoefficientsAtLowAOA(aoaStallP, aoaZero, correctedLiftSlope, aspectRatio).ToVector3();
                        aerodynamicCoefficientsStall = CalculateCoefficientsAtHighAOA(
                            paddedStallAngleHigh, aoaStallP, aoaStallN, aoaZero, correctedLiftSlope, aspectRatio).ToVector3();
                        lerpParam = (aoaRad - aoaStallP) / (paddedStallAngleHigh - aoaStallP);
                    }
                    else
                    {
                        aerodynamicCoefficientsLow = CalculateCoefficientsAtLowAOA(aoaStallN, aoaZero, correctedLiftSlope, aspectRatio).ToVector3();
                        aerodynamicCoefficientsStall = CalculateCoefficientsAtHighAOA(
                            paddedStallAngleLow, aoaStallP, aoaStallN, aoaZero, correctedLiftSlope, aspectRatio).ToVector3();
                        lerpParam = (aoaRad - aoaStallN) / (paddedStallAngleLow - aoaStallN);
                    }
                    coefficients = LDMCoefficients.FromVector3(Vector3.Lerp(aerodynamicCoefficientsLow, aerodynamicCoefficientsStall, lerpParam));
                }
            }

            return coefficients;
        }

        LDMCoefficients CalculateCoefficientsAtLowAOA(float aoaRad, float aoaZero, float correctedLiftSlope, float AR)
        {
            float CL = correctedLiftSlope * (aoaRad - aoaZero); //Lift Coefficient
            float aInd = CL / (Mathf.PI * AR); //Induced AoA(诱导迎角)
            float aEff = aoaRad - aoaZero - aInd; //Effective Angle
            float CT = skinFrictionCoiffient * Mathf.Cos(aEff); //Tangential Coefficient
            float CN = (CL + CT * Mathf.Sin(aEff)) / Mathf.Cos(aEff); //Normal Coefficient
            float CD = CN * Mathf.Sin(aEff) + CT * Mathf.Cos(aEff); //Drag Coefficient
            float CM = -CN * (0.25f - 0.175f * (1 - 2 * Mathf.Abs(aEff) / Mathf.PI)); //Torque Coefficient

            return new LDMCoefficients(CL, CD, CM);
        }

        LDMCoefficients CalculateCoefficientsAtHighAOA(float aoaRad, float aoaStallP, float aoaStallN, float aoaZero, float correctedLiftSlope, float AR)
        {
            float CdZero = skinFrictionCoiffient;

            float liftCoefficientLowAoA;
            if (aoaRad > aoaStallP)
            {
                liftCoefficientLowAoA = correctedLiftSlope * (aoaStallP - aoaZero);
            }
            else
            {
                liftCoefficientLowAoA = correctedLiftSlope * (aoaStallN - aoaZero);
            }
            float inducedAngle = liftCoefficientLowAoA / (Mathf.PI * AR);

            float lerpParam;
            if (aoaRad > aoaStallP)
            {
                lerpParam = (Mathf.PI / 2 - Mathf.Clamp(aoaRad, -Mathf.PI / 2, Mathf.PI / 2))
                    / (Mathf.PI / 2 - aoaStallP);
            }
            else
            {
                lerpParam = (-Mathf.PI / 2 - Mathf.Clamp(aoaRad, -Mathf.PI / 2, Mathf.PI / 2))
                    / (-Mathf.PI / 2 - aoaStallN);
            }
            inducedAngle = Mathf.Lerp(0, inducedAngle, lerpParam);
            float effectiveAngle = aoaRad - aoaZero - inducedAngle;

            float CdN = -4.26e-2f * flapAngle * flapAngle + 2.1e-1f * flapAngle + 1.98f; // Cd,90
            float normalCoefficient = CdN * Mathf.Sin(effectiveAngle) *
                (1 / (0.56f + 0.44f * Mathf.Abs(Mathf.Sin(effectiveAngle))) -
                0.41f * (1 - Mathf.Exp(-17 / AR)));
            float tangentialCoefficient = 0.5f * CdZero * Mathf.Cos(effectiveAngle);

            float liftCoefficient = normalCoefficient * Mathf.Cos(effectiveAngle) - tangentialCoefficient * Mathf.Sin(effectiveAngle);
            float dragCoefficient = normalCoefficient * Mathf.Sin(effectiveAngle) + tangentialCoefficient * Mathf.Cos(effectiveAngle);
            float torqueCoefficient = -normalCoefficient * Mathf.Abs(0.25f - 0.175f * (1 - 2 * effectiveAngle / Mathf.PI));

            //torqueCoefficient = Mathf.Abs(0.25f - 0.175f * (1 - 2 * effectiveAngle / Mathf.PI));
            //torqueCoefficient = -normalCoefficient;

            return new LDMCoefficients(liftCoefficient, dragCoefficient, torqueCoefficient);
        }
    }

    [Serializable]
    public class ControlSurfaceSettings
    {
        public AeroSurface.ControlSurfaceType surfaceType = AeroSurface.ControlSurfaceType.None;
        public bool invertInput = false;
        public float anglePercent;

        public float GetAngle(Vector3 input)
        {
            switch (surfaceType)
            {
                case ControlSurfaceType.Yaw:
                    return anglePercent * input.x * (invertInput ? -1 : 1);
                case ControlSurfaceType.Pitch:
                    return anglePercent * input.y * (invertInput ? -1 : 1);
                case ControlSurfaceType.Roll:
                    return anglePercent * input.z * (invertInput ? -1 : 1);
            }

            return 0;
        }
    }
}