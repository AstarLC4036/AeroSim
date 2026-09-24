using AeroSim.AircraftModules;
using AeroSim.InputSystem;
using System.Collections;
using UnityEngine;
using static AeroSim.InputSystem.FlightController;

namespace AeroSim.InputSystem
{
    public class MissileFlightController : MonoBehaviour
    {
        public Missile missile;
        public PID rollPID;
        public PID pitchPID;
        public PID yawPID;
        public bool debug;
        public Vector3 dirDebug;
        public float referenceSpeed = 300f;

        public Vector3 CalcInput(Vector3 desiredDirection, float dt)
        {
            Vector3 desiredDir;
            if (!debug)
            {
                desiredDir = desiredDirection;
            }
            else
            {
                desiredDir = dirDebug;
            }

            Vector3 angularVelocity = missile.rb.angularVelocity;
            Vector3 localAngularVelo = transform.InverseTransformDirection(angularVelocity);
            float rollRate = localAngularVelo.z;
            float pitchRate = localAngularVelo.x;
            float yawRate = localAngularVelo.y;

            float speedFactor = Mathf.Clamp(referenceSpeed * referenceSpeed / Mathf.Max(missile.rb.velocity.magnitude * missile.rb.velocity.magnitude, 1f), 0.05f, 1.0f);
            rollPID.factors = pitchPID.factors = yawPID.factors = new Vector3(speedFactor, 1f, speedFactor);

            // 转为机体坐标
            Vector3 localDir = transform.InverseTransformDirection(desiredDir);

            // 映射到三轴输入（-1 到 1）
            float pitchCmd = pitchPID.Update(dt, localDir.y, pitchRate);
            float yawCmd = yawPID.Update(dt, localDir.x, yawRate);
            float rollCmd = CalculateRollStabilization();

            Vector3 result = new Vector3(yawCmd, pitchCmd, rollCmd);
            //// 直接设置 controllingInput
            //Vector3 result = Control(dt, desiredDir);

            Debug.DrawLine(transform.position, transform.position + desiredDir * 10, Color.yellow);
            return result;
        }

        float CalculateRollStabilization()
        {
            // 滚转稳定：使 up 趋向 world up
            float rollError = Vector3.SignedAngle(transform.up, Vector3.up, transform.forward);
            float rollRate = Vector3.Dot(missile.rb.angularVelocity, transform.forward);

            float rollCmd = -rollPID.kp * rollError - rollPID.kd * rollRate;

            return Mathf.Clamp(rollCmd / 30f, -1f, 1f);
        }
    }
}