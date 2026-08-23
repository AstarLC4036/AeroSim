using AeroSim.AeroPhysics;
using AeroSim.Utility;
using System;
using UnityEngine;
using static AeroSim.Utility.MathUtility;

namespace AeroSim.InputSystem
{
    public class FlightController : MonoBehaviour
    {
        [Serializable]
        public class PID
        {
            public float kp;
            public float ki;
            public float kd;
            public Vector3 factors = Vector3.one;
            private float integral;
            private float previousError;
            public float iMin;
            public float iMax;

            public float Update(float dt, float error)
            {
                integral += error * dt;
                integral = Mathf.Clamp(integral, iMin, iMax);
                float dError = (error - previousError) / dt;
                previousError = error;
                float result = kp * factors.x * error + ki * factors.y * integral + kd * factors.z * dError;
                return result;
            }

            public float Update(float dt, float error, float dError)
            {
                integral += error * dt;
                integral = Mathf.Clamp(integral, iMin, iMax);
                float result = kp * factors.x * error + ki * factors.y * integral + kd * factors.z * dError;
                return result;
            }
        }

        //private static FlightController instance;
        //public static FlightController Instance => instance;

        public float sensitivity = 0.5f;
        //public static Vector3 input = Vector3.zero;
        //public float returnCoeff = 1.5f;

        [Header("Aim Ring(PID)")]
        public PID rollPID;
        public PID pitchPID;
        public PID yawPID;
        public PID rollRatePID;
        public PID pitchRatePID;
        public PID yawRatePID;
        public Vector2 pitchRateLimit;
        public Vector2 yawRateLimit;
        //public float kpRoll;
        //public float kdRoll;
        //public float kpPitch;
        //public float kiPitch;
        //public float kdPitch;
        //public float kpYaw;
        //public float kdYaw;
        //public float kpTurnYaw;
        //public float kpAdverseYaw;
        //public float kpSideslieYaw;

        //public AnimationCurve maxRollAngle;

        //public float kpRollOut;
        //public float maxRollRate;
        //public float kpRollRate;
        //public float kiRollRate;
        //public float kdRollRate;
        //[SerializeField]
        //private float rollRateIntegral;
        //private float pitchIntegral;

        [Header("Stablilization")]
        public float levelModeThresholdDeg;
        public float beginRollThresholdDeg;
        public float levelModeBlendDeg;
        public float yawThresholdDeg = 3f;
        public float rollGain;
        public float rollLevelStableGain;
        public float referenceSpeed;
        public float generalFactor;
        public Vector3 damper;

        [Header("Averge Input")]
        public int avgSize;
        private Vector3[] previousValues;
        private int previousValueIndex;
        private int actualValueCount;

        private Aircraft aircraft;

        private static bool isOperationMode = false;
        public static bool IsOperationMode => isOperationMode;

        public void Init(Aircraft aircraft)
        {
            this.aircraft = aircraft;
        }

        private void Awake()
        {
            //instance = this;
        }

        public void Start()
        {
            aircraft = Aircraft.main;

            previousValues = new Vector3[avgSize];
            previousValueIndex = 0;
        }

        public void Update()
        {
            if(!isOperationMode && Cursor.visible)
            {
                Cursor.visible = false;
            }
            else if (isOperationMode && !Cursor.visible)
            {
                Cursor.visible = true;
            }

            if(aircraft.isControlling)
            {
                if (!Input.GetKey(Keybindings.holdControlInput) && !CameraController.Instance.enableStable)
                {
                    Vector3 mouseDelta = Input.mousePositionDelta;
                    aircraft.targetDir = RotateRound(aircraft.targetDir, Vector3.zero, Camera.main.transform.up, mouseDelta.x * sensitivity * Time.deltaTime);
                    aircraft.targetDir = RotateRound(aircraft.targetDir, Vector3.zero, Camera.main.transform.right, -mouseDelta.y * sensitivity * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// 计算瞄准环瞄准
        /// </summary>
        public Vector3 AimRingControl(Vector3 targetDir, float dt)
        {
            Vector3 localTargetDir = aircraft.transform.InverseTransformDirection(targetDir).normalized;
            Vector3 targetHorziontal = Vector3.ProjectOnPlane(targetDir, Vector3.up).normalized;
            Vector3 forwardHorziontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            // Error Calculation (in radains)
            float targetRollAngle = CovertAngle(Mathf.Atan2(localTargetDir.y, localTargetDir.x) * Mathf.Rad2Deg - 90);
            float rollError = Mathf.Atan2(localTargetDir.x, localTargetDir.z);
            float pitchError = Mathf.Atan2(localTargetDir.y, Mathf.Sqrt(localTargetDir.x * localTargetDir.x + localTargetDir.z * localTargetDir.z));
            float yawError = Vector3.SignedAngle(forwardHorziontal, targetHorziontal, Vector3.up) * Mathf.Deg2Rad;
            float errorDeg = Vector3.Angle(Vector3.forward, localTargetDir);

            // Aircarft Status
            Vector3 localAngularVelo = aircraft.transform.InverseTransformDirection(aircraft.Rb.angularVelocity);
            float rollRate = localAngularVelo.z;
            float pitchRate = localAngularVelo.x;
            float yawRate = localAngularVelo.y;
            float currentRollAngle = CovertAngle(transform.eulerAngles.z) * Mathf.Deg2Rad;
            float currentSpeed = aircraft.Velocity.magnitude;

            // Factors
            Vector3 factors = new Vector3(generalFactor, 1, generalFactor);
            yawRatePID.factors = yawPID.factors = pitchRatePID.factors = pitchPID.factors = rollRatePID.factors = rollPID.factors = factors;

            // Pitch
            float desiredPitchRate = Mathf.Clamp(pitchPID.Update(dt, pitchError, pitchRate), pitchRateLimit.x * Mathf.Deg2Rad, pitchRateLimit.y * Mathf.Deg2Rad);
            float pitchCmd = pitchRatePID.Update(dt, desiredPitchRate - pitchRate);

            // Roll
            // - Level Stablilization
            // 感谢群友的指导，群友的恩情还不完
            float targetRoll = Mathf.Lerp(localTargetDir.x, CovertAngle(transform.localEulerAngles.z) * Mathf.Deg2Rad, Mathf.Clamp01(localTargetDir.z - beginRollThresholdDeg) * rollLevelStableGain) * rollGain;
            float desiredRollRate = rollPID.Update(dt, targetRoll, rollRate);
            // - Final command
            float rollCmd = rollRatePID.Update(dt, desiredRollRate - rollRate);

            float blend = 1 - Mathf.Clamp01((errorDeg - levelModeThresholdDeg) / levelModeBlendDeg);
            float rollLevelStableCmd = currentRollAngle;

            // Yaw
            float desiredYawRate = Mathf.Clamp(yawPID.Update(dt, yawError, yawRate), yawRateLimit.x * Mathf.Deg2Rad, yawRateLimit.y * Mathf.Deg2Rad);
            float yawCmd = yawRatePID.Update(dt, desiredYawRate - yawRate);

            Vector3 inputResult = new Vector3(yawCmd, pitchCmd, rollCmd);
            inputResult += Vector3.Scale(localAngularVelo, damper);
            Vector3 filteredResult = UpdateAvgInput(inputResult);

            //Debug.Log($"desired pitch rate: {desiredPitchRate}, pitch: {pitchCmd}, roll target {targetRollAngle}, stable blend {blend}, error deg: {errorDeg}, result {inputResult}");

            return filteredResult;
        }

        private Vector3 UpdateAvgInput(Vector3 value)
        {
            previousValues[previousValueIndex] = value;

            if (actualValueCount <= previousValues.Length)
                actualValueCount++;

            Vector3 sum = previousValues[previousValueIndex];
            for(int i = 0; i < actualValueCount; i++)
            {
                int index = (previousValueIndex + i) % previousValues.Length;
                sum += previousValues[index];
            }
            Vector3 avg = sum / actualValueCount;

            previousValueIndex++;

            if (previousValueIndex >= previousValues.Length)
                previousValueIndex = 0;

            return avg;
        }

        // rubbish bin blow this line

        ///// <summary>
        ///// 通过给定的方向计算输入
        ///// </summary>
        ///// <param name="aircraft">操作的Aircraft目标</param>
        ///// <param name="targetDir">目标方向</param>
        ///// <returns>控制输入</returns>
        //public static Vector2 AnimRingControl(Aircraft aircraft, Vector3 targetDir)
        //{
        //    Vector3 localTargetDir = aircraft.transform.InverseTransformDirection(targetDir);

        //    float rollError = Mathf.Atan2(localTargetDir.x, localTargetDir.z);
        //    float pitchError = Mathf.Atan2(localTargetDir.y, Mathf.Sqrt(localTargetDir.x * localTargetDir.x + localTargetDir.z * localTargetDir.z));

        //    Vector3 localAngularVelo = aircraft.transform.InverseTransformDirection(aircraft.Rb.angularVelocity);
        //    float rollRate = localAngularVelo.z;
        //    float pitchRate = localAngularVelo.x;

        //    float rollInput, pitchInput;

        //    float absRollError = Mathf.Abs(rollError);
        //    float rollCmd = instance.kpRoll * rollError - instance.kdRoll * rollRate;

        //    //level stablilization
        //    float currentRollAngle = Vector3.SignedAngle(aircraft.transform.up, Vector3.up, aircraft.transform.forward) * Mathf.Deg2Rad;
        //    float levelRollCmd;
        //    if (Mathf.Abs(currentRollAngle) > 3)
        //    {
        //        levelRollCmd = instance.kpLevel * (-currentRollAngle) - instance.kdLevel * rollRate;
        //    }
        //    else
        //    {
        //        levelRollCmd = -instance.kdLevel * rollRate;
        //    }
        //    float t = 1f - Mathf.Clamp01(absRollError / instance.levelModeThreshold);

        //    rollInput = Mathf.Lerp(rollCmd, levelRollCmd, t);

        //    //float desiredRate = 30;
        //    //float rateError = desiredRate - rollRate * Mathf.Rad2Deg;

        //    pitchInput = instance.kpPitch * -pitchError - instance.kdPitch * pitchRate;

        //    return new Vector2(rollInput, pitchInput);
        //}

        //private void SetInput(Vector2 input)
        //{
        //    float clampedX = Mathf.Clamp(input.x, -1, 1);
        //    float clampedY = Mathf.Clamp(input.y, -1, 1);
        //    FlightController.input = new Vector3(clampedX, clampedY);
        //}

        //float hsSpeedFactor = Mathf.Clamp(referenceSpeed / Mathf.Max(currentSpeed, 1f), 0.2f, 1.0f);
        //float lsSpeedFactor = Mathf.Clamp(Mathf.Max(currentSpeed, 1f) / referenceSpeed, 0.2f, 1.0f);
        //Vector3 factors = new Vector3(hsSpeedFactor * lsSpeedFactor, 1, hsSpeedFactor * lsSpeedFactor);

        //float finalRollError = Mathf.Lerp(-targetRollAngle * Mathf.Deg2Rad, currentRollAngle, blend);
        //float finalRollError = Mathf.Lerp(rollError, currentRollAngle, blend);
        //float baseRollCmd = rollPID.Update(dt, finalRollError, rollRate);

        //float baseYawCmd = yawPID.Update(dt, yawError, yawRate);
        //float slideYawCmd = yawRatePID.Update(dt, yawRateError);
        //float blendYawCmd = baseRollCmd * 0.4f + Mathf.Lerp(baseYawCmd, slideYawCmd, Mathf.Clamp01(yawError / yawThresholdDeg)) * 0.6f;

        ////   level stablilization
        //float currentRollAngle = Vector3.SignedAngle(aircraft.transform.up, Vector3.up, aircraft.transform.forward);
        //float targetRollAngle = maxRollAngle.Evaluate(rollError * Mathf.Rad2Deg);
        //float sideslipAngle = Vector3.SignedAngle(transform.forward, aircraft.Velocity.normalized, transform.up);

        //float angleError = targetRollAngle - currentRollAngle;
        //float desiredRate = Mathf.Clamp(kpRollOut * angleError, -maxRollRate, maxRollRate);

        //float rateError = desiredRate - rollRate * Mathf.Rad2Deg;
        //rollRateIntegral += rateError * dt;
        //rollRateIntegral = Mathf.Clamp(rollRateIntegral, -0.15f, 0.15f);
        //float levelRollCmd = kpRollRate * rateError + kiRollRate * rollRateIntegral + kdRollRate * rollRate;

        //if(currentRollAngle < 1)
        //{
        //    levelRollCmd = 0;
        //}

        //float t = 1f - Mathf.Clamp01(absRollError / levelModeThreshold);
        //rollInput = Mathf.Lerp(baseRollCmd, Mathf.Clamp(levelRollCmd, -1, 1), t);

        //// pitch
        //pitchIntegral += pitchError * dt;
        //pitchIntegral = Mathf.Clamp(pitchIntegral, -0.15f, 0.15f);

        //pitchInput = kpPitch * -pitchError + pitchIntegral * kiPitch - kdPitch * pitchRate;

        //// yaw
        //float yawCmd = kpYaw * yawError - kdYaw * yawRate;

        //float targetYawRate = 0;
        //if(Mathf.Abs(currentRollAngle) > 1f && currentSpeed > 10)
        //{
        //    targetYawRate = 9.81f * Mathf.Tan(currentRollAngle * Mathf.Deg2Rad) / currentSpeed;
        //    //targetYawRate *= Mathf.Rad2Deg;
        //}
        //float yawRateError = targetYawRate - yawRate;
        //float coordTurnCmd = kpTurnYaw * yawRateError;

        //float adverseYawCmd = rollInput * kpAdverseYaw;

        //float sideslideCmd = sideslipAngle * kpSideslieYaw;

        //float yawInput = yawCmd + coordTurnCmd + adverseYawCmd + sideslideCmd;

        //return new Vector3(yawInput, pitchInput, rollInput);
    }
}