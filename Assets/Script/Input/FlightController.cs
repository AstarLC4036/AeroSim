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

        [Header("Level Stablilization")]
        public float levelModeThresholdDeg = 3f;
        public float yawThresholdDeg = 3f;

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
            Vector3 localTargetDir = aircraft.transform.InverseTransformDirection(targetDir);
            Vector3 targetHorziontal = Vector3.ProjectOnPlane(targetDir, Vector3.up).normalized;
            Vector3 forwardHorziontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            // error calculation (in radains)
            float rollError = Mathf.Atan2(localTargetDir.x, localTargetDir.z);
            float pitchError = Mathf.Atan2(localTargetDir.y, Mathf.Sqrt(localTargetDir.x * localTargetDir.x + localTargetDir.z * localTargetDir.z));
            float yawError = Vector3.SignedAngle(forwardHorziontal, targetHorziontal, Vector3.up) * Mathf.Deg2Rad;

            // aircarft status
            Vector3 localAngularVelo = aircraft.transform.InverseTransformDirection(aircraft.Rb.angularVelocity);
            float rollRate = localAngularVelo.z;
            float pitchRate = localAngularVelo.x;
            float yawRate = localAngularVelo.y;

            float currentSpeed = aircraft.Velocity.magnitude;

            float currentRollAngle = CovertAngle(transform.eulerAngles.z) * Mathf.Deg2Rad;
            float angleOffTarget = Mathf.Abs(rollError);

            float speedFactor = Mathf.Clamp(165 / Mathf.Max(currentSpeed, 1f), 0.2f, 1.2f); // 165 -> reference speed
            Vector3 factors = new Vector3(speedFactor, 1, speedFactor);
            pitchRatePID.factors = pitchPID.factors = rollPID.factors = factors;

            // level stablilization
            float blend = 1f - Mathf.Clamp(angleOffTarget / (levelModeThresholdDeg * Mathf.Deg2Rad), 0.1f, 1);
            float finalRollError = Mathf.Lerp(rollError, currentRollAngle, blend);

            float baseRollCmd = rollPID.Update(dt, finalRollError, rollRate);

            float desiredPitchRate = Mathf.Clamp(pitchPID.Update(dt, pitchError), pitchRateLimit.x * Mathf.Deg2Rad, pitchRateLimit.y * Mathf.Deg2Rad);
            float pitchCmd = pitchRatePID.Update(dt, desiredPitchRate - pitchRate);

            //float basePitchCmd = pitchPID.Update(dt, pitchError, pitchRate);

            float desiredYawRate = Mathf.Clamp(yawPID.Update(dt, yawError), yawRateLimit.x * Mathf.Deg2Rad, yawRateLimit.y * Mathf.Deg2Rad);
            float yawCmd = yawPID.Update(dt, desiredYawRate - yawRate);
            //float baseYawCmd = yawPID.Update(dt, yawError, yawRate);
            //float slideYawCmd = yawRatePID.Update(dt, yawRateError);
            //float blendYawCmd = baseRollCmd * 0.4f + Mathf.Lerp(baseYawCmd, slideYawCmd, Mathf.Clamp01(yawError / yawThresholdDeg)) * 0.6f;

            Vector3 inputResult = new Vector3(yawCmd, pitchCmd, baseRollCmd);
            Vector3 filteredResult = UpdateAvgInput(inputResult);

            return filteredResult;

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
    }
}