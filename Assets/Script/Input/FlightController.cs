using AeroSim.AeroPhysics;
using System;
using UnityEngine;
using static AeroSim.Utils.MathUtility;

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

            public static float Direct(float kp, float ki, float kd, float error, float integral, float dError)
            {
                return kp * error + ki * integral + kd * dError;
            }
        }

        [Serializable]
        public class Vector3PID
        {
            public float kp;
            public float ki;
            public float kd;
            public Vector3 factors = Vector3.one;
            private Vector3 integral;
            private Vector3 previousError;
            public float iMax;

            public Vector3 Update(float dt, Vector3 error)
            {
                integral += error * dt;
                if (integral.magnitude > iMax)
                    integral = integral.normalized * iMax;
                Vector3 dError = (error - previousError) / dt;
                previousError = error;
                Vector3 result = kp * factors.x * error + ki * factors.y * integral + kd * factors.z * dError;
                return result;
            }

            public Vector3 Update(float dt, Vector3 error, Vector3 dError)
            {
                integral += error * dt;
                if(integral.magnitude > iMax)
                    integral = integral.normalized * iMax;
                Vector3 result = kp * factors.x * error + ki * factors.y * integral + kd * factors.z * dError;
                return result;
            }
        }

        public float sensitivity = 0.5f;

        [Header("Missile control")]
        public Rigidbody rb;

        [Header("Aim Ring(PID)")]
        public PID rollPID;
        public PID pitchPID;
        public PID yawPID;
        public PID rollRatePID;
        public PID pitchRatePID;
        public PID yawRatePID;
        public Vector2 pitchRateLimit;
        public Vector2 yawRateLimit;

        [Header("Stablilization")]
        public float levelModeThresholdDeg;
        public float beginRollThresholdDeg;
        public float rollGain;
        public float rollLevelStableGain;
        public float generalFactor;
        public Vector3 damper;

        [Header("Filter")]
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
            //aircraft = Aircraft.main;

            previousValues = new Vector3[avgSize];
            previousValueIndex = 0;
        }

        public void Update()
        {
            if (!isOperationMode)
            {
                if(Cursor.lockState != CursorLockMode.Confined)
                    Cursor.lockState = CursorLockMode.Confined;
                
                if(Cursor.visible)
                    Cursor.visible = false;
            }
            else if (isOperationMode)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                    Cursor.lockState = CursorLockMode.Locked;

                if (!Cursor.visible)
                    Cursor.visible = true;
            }

            if(aircraft != null && aircraft.isControlling)
            {
                if (!CameraController.Instance.enableMoveCameraDirectly && CameraController.CurrentView.view != CameraController.CameraView.ViewType.Pod)
                {
                    Vector3 mouseDelta = Input.mousePositionDelta;
                    aircraft.targetDir = RotateRound(aircraft.targetDir, Vector3.zero, Camera.main.transform.up, Keybindings.smoothMouseDelta.x * sensitivity);
                    aircraft.targetDir = RotateRound(aircraft.targetDir, Vector3.zero, Camera.main.transform.right, -Keybindings.smoothMouseDelta.y * sensitivity);
                }
            }
        }

        /// <summary>
        /// 计算瞄准环瞄准
        /// </summary>
        public Vector3 AimRingControl(Vector3 targetDir, float dt)
        {
            Vector3 localTargetDir = transform.InverseTransformDirection(targetDir).normalized;
            Vector3 targetHorziontal = Vector3.ProjectOnPlane(targetDir, Vector3.up).normalized;
            Vector3 forwardHorziontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            // Error Calculation (in radains)
            float targetRollAngle = CovertAngle(Mathf.Atan2(localTargetDir.y, localTargetDir.x) * Mathf.Rad2Deg - 90);
            float rollError = Mathf.Atan2(localTargetDir.x, localTargetDir.z);
            float pitchError = Mathf.Atan2(localTargetDir.y, Mathf.Sqrt(localTargetDir.x * localTargetDir.x + localTargetDir.z * localTargetDir.z));
            float yawError = Vector3.SignedAngle(forwardHorziontal, targetHorziontal, Vector3.up) * Mathf.Deg2Rad;
            float errorDeg = Vector3.Angle(Vector3.forward, localTargetDir);

            // Aircarft Status
            Vector3 localAngularVelo = transform.InverseTransformDirection(rb.angularVelocity);
            float rollRate = localAngularVelo.z;
            float pitchRate = localAngularVelo.x;
            float yawRate = localAngularVelo.y;
            float currentRollAngle = CovertAngle(transform.eulerAngles.z) * Mathf.Deg2Rad;
            float currentSpeed = rb.velocity.magnitude;

            // Factors
            Vector3 factors = new Vector3(generalFactor, 1, 1);
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

            // Yaw
            float desiredYawRate = Mathf.Clamp(yawPID.Update(dt, rollError, yawRate), yawRateLimit.x * Mathf.Deg2Rad, yawRateLimit.y * Mathf.Deg2Rad);
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

        // --- Alternative aim ring controller -------------------------------------------------------
        // Purely additive: AimRingControl above is untouched. This one reuses its PID objects, its
        // gains, its limits and its sign conventions, so whatever was tuned (or inverted) in the
        // inspector for the first version still applies here. To try it, change the single call in
        // Aircraft.UpdateInput:
        //     controllingInput = flightController.AimRingControlAlternative(targetDir, dt);
        //
        // The only real change is process 303A of US 9,858,830 B2 (Gaijin Entertainment): the errors
        // are evaluated at a PREDICTED attitude instead of the current one. That prediction is what the
        // patent uses in place of a derivative or damper term.

        [Header("Aim Ring Alternative - predicted attitude")]
        [Tooltip("Prediction steps. 0 evaluates the errors at the current attitude, i.e. the first version.")]
        public int altPredictionSteps = 6;
        [Tooltip("Seconds advanced per prediction step. Steps x step time is the total lead time.")]
        public float altPredictionStepTime = 0.02f;
        [Tooltip("Include the measured angular acceleration in the prediction.")]
        public bool altUseAngularAcceleration = true;
        [Tooltip("Drive the rudder from the lateral component (patent 407) instead of atan2(x, z).")]
        public bool altYawLateralError = true;
        [Tooltip("Rate error deadband in degrees per second. Kills the level flight trim buzz.")]
        public float altRateDeadband = 0.5f;
        [Tooltip("First order smoothing time constant for the aim direction, in seconds. 0 disables it.")]
        public float altAimSmoothing = 0.05f;
        [Tooltip("Average the output over this many frames. 0 or 1 turns it off.")]
        public int altAverageSize;
        [Tooltip("Opt in protection: the patent has none, it simply lets the aircraft stall.")]
        public bool altProtectLimits;
        public float altMaxAoaDeg = 26f;
        public float altMaxG = 9f;

        private Vector3 previousAngularVelocity;
        private Vector3 smoothedTargetDir;
        private Vector3[] altPreviousValues;
        private int altPreviousValueIndex;
        private int altAverageCount;

        /// <summary>
        /// The first version's control law with the errors evaluated at a predicted attitude, the way
        /// process 303A of US 9,858,830 B2 does it.
        /// <para>Same PID objects, same gains, same limits and same sign conventions as AimRingControl,
        /// so inspector tuning carries over. With altPredictionSteps at 0 the error computation reduces
        /// to the first version's; the only other differences are that the output average is opt in and,
        /// when enabled, computed correctly.</para>
        /// </summary>
        public Vector3 AimRingControlAlternative(Vector3 targetDir, float dt)
        {
            // actually this should be directly apply to the target dir
            // --- optional smoothing of the aim direction -------------------------------------------
            // The raw target is integrated from mouse deltas once per frame, so its jitter and the
            // frame rate dependent step go straight into the loops. A first order lag here removes that
            // excitation. It cannot cure a buzz that happens with the mouse untouched: that one is
            // generated inside the loops themselves.
            Vector3 aimDir = targetDir;

            //if (altAimSmoothing > 0f)
            //{
            //    if (smoothedTargetDir == Vector3.zero) smoothedTargetDir = targetDir;

            //    smoothedTargetDir = Vector3.Lerp(smoothedTargetDir, targetDir,
            //                                     1f - Mathf.Exp(-dt / altAimSmoothing)).normalized;
            //    aimDir = smoothedTargetDir;
            //}

            // --- process 303A: predict the attitude first, then read the errors there ---------------
            // The first version read the target direction in the body frame of the CURRENT attitude, so
            // the roll channel commanded against the roll it was itself causing: rolling rotates the
            // target direction in the body frame, which self excites on a target that sits abeam.
            // Predicting the attitude forward removes that loop, and that lead is exactly what the
            // patent relies on instead of a derivative or damper term.
            Vector3 angularVelocity = rb.angularVelocity;                       // world space
            Vector3 angularAcceleration = altUseAngularAcceleration
                ? (angularVelocity - previousAngularVelocity) / dt
                : Vector3.zero;
            previousAngularVelocity = angularVelocity;

            Quaternion predictedOrientation = transform.rotation;
            Vector3 localTargetDir = transform.InverseTransformDirection(aimDir).normalized;

            for (int i = 0; i < altPredictionSteps; i++)
            {
                Vector3 predictedAngularVelocity =
                    angularVelocity + angularAcceleration * ((i + 1) * altPredictionStepTime);

                float stepDegrees = predictedAngularVelocity.magnitude * altPredictionStepTime * Mathf.Rad2Deg;

                if (stepDegrees > 1e-4f)
                {
                    predictedOrientation =
                        Quaternion.AngleAxis(stepDegrees, predictedAngularVelocity / predictedAngularVelocity.magnitude) *
                        predictedOrientation;
                }

                localTargetDir = Quaternion.Inverse(predictedOrientation) * aimDir;
            }

            localTargetDir.Normalize();

            // --- the first version's errors, now evaluated at the predicted attitude ----------------
            // The first version fed atan2(x, z) to the RUDDER. That angle is very sensitive around the
            // aim point (its slope is 1/z there), which is exactly where a level flight trim buzz lives,
            // and it also makes the rudder chase the same lateral offset the ailerons are already
            // nulling - with the highest inner loop gain on the aircraft. Process 407 of the patent uses
            // the lateral component itself, which is flat at the aim point instead.
            float rollError = altYawLateralError
                ? localTargetDir.x
                : Mathf.Atan2(localTargetDir.x, localTargetDir.z);
            float pitchError = Mathf.Atan2(localTargetDir.y,
                                            Mathf.Sqrt(localTargetDir.x * localTargetDir.x +
                                                       localTargetDir.z * localTargetDir.z));

            // Aircraft status stays the real one: the rate loops need what is happening right now.
            Vector3 localAngularVelo = transform.InverseTransformDirection(rb.angularVelocity);
            float rollRate = localAngularVelo.z;
            float pitchRate = localAngularVelo.x;
            float yawRate = localAngularVelo.y;

            Vector3 factors = new Vector3(generalFactor, 1, 1);
            yawRatePID.factors = yawPID.factors = pitchRatePID.factors = pitchPID.factors =
                rollRatePID.factors = rollPID.factors = factors;

            // Pitch
            float desiredPitchRate = Mathf.Clamp(pitchPID.Update(dt, pitchError, pitchRate),
                                                 pitchRateLimit.x * Mathf.Deg2Rad,
                                                 pitchRateLimit.y * Mathf.Deg2Rad);

            // Opt in protection. Nose up is a negative rate in this convention, so once a limit is
            // reached only the unloading direction stays available.
            if (altProtectLimits && aircraft != null &&
                (aircraft.AOA > altMaxAoaDeg || aircraft.G > altMaxG))
            {
                desiredPitchRate = Mathf.Max(desiredPitchRate, 0f);
            }

            float pitchCmd = pitchRatePID.Update(dt, Deadband(desiredPitchRate - pitchRate));

            // Roll
            float targetRoll = Mathf.Lerp(localTargetDir.x,
                                          CovertAngle(transform.localEulerAngles.z) * Mathf.Deg2Rad,
                                          Mathf.Clamp01(localTargetDir.z - beginRollThresholdDeg) *
                                          rollLevelStableGain) * rollGain;
            float desiredRollRate = rollPID.Update(dt, targetRoll, rollRate);
            float rollCmd = rollRatePID.Update(dt, Deadband(desiredRollRate - rollRate));

            // Yaw
            float desiredYawRate = Mathf.Clamp(yawPID.Update(dt, rollError, yawRate),
                                               yawRateLimit.x * Mathf.Deg2Rad,
                                               yawRateLimit.y * Mathf.Deg2Rad);
            float yawCmd = yawRatePID.Update(dt, Deadband(desiredYawRate - yawRate));

            Vector3 inputResult = new Vector3(yawCmd, pitchCmd, rollCmd);
            inputResult += Vector3.Scale(localAngularVelo, damper);

            return altAverageSize > 1 ? UpdateAlternativeAvgInput(inputResult) : inputResult;
        }

        /// <summary>
        /// Soft deadband on a rate error: nothing inside the band, and no discontinuity at its edge, so
        /// a loop that only has to hold trim stops hunting without losing its response to real inputs.
        /// </summary>
        private float Deadband(float rateError)
        {
            float band = altRateDeadband * Mathf.Deg2Rad;

            return Mathf.Abs(rateError) < band ? 0f : rateError - Mathf.Sign(rateError) * band;
        }

        /// <summary>
        /// Same idea as the first version's averaging, but the newest sample is counted once and the sum
        /// is divided by the number of samples actually stored, so the gain does not change while the
        /// buffer fills up.
        /// </summary>
        private Vector3 UpdateAlternativeAvgInput(Vector3 value)
        {
            if (altPreviousValues == null || altPreviousValues.Length != altAverageSize)
            {
                altPreviousValues = new Vector3[altAverageSize];
                altPreviousValueIndex = 0;
                altAverageCount = 0;
            }

            altPreviousValues[altPreviousValueIndex] = value;
            altPreviousValueIndex = (altPreviousValueIndex + 1) % altPreviousValues.Length;

            if (altAverageCount < altPreviousValues.Length) altAverageCount++;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < altAverageCount; i++) sum += altPreviousValues[i];

            return sum / altAverageCount;
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

        //private static FlightController instance;
        //public static FlightController Instance => instance;

        //public static Vector3 input = Vector3.zero;
        //public float returnCoeff = 1.5f;

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

        //public float levelModeBlendDeg;
        //public float yawThresholdDeg = 3f;
        //public float referenceSpeed;

        //float blend = 1 - Mathf.Clamp01((errorDeg - levelModeThresholdDeg) / levelModeBlendDeg);
        //float rollLevelStableCmd = currentRollAngle;

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