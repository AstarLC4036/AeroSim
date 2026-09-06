using AeroSim.AircraftModules;
using AeroSim.InputSystem;
using System.Collections;
using UnityEngine;

namespace AeroSim.InputSystem
{
    public class MissileFlightController : MonoBehaviour
    {
        public Missile missile;
        public float maxG = 30f;
        public FlightController.PID rollPID;
        public FlightController.PID yawPID;
        public FlightController.PID pitchPID;

        public Vector3 CalcInput(Vector3 commandAccel)
        {
            commandAccel = Vector3.ClampMagnitude(commandAccel, maxG * 9.81f);

            Vector3 angularVelocity = missile.rb.angularVelocity;
            Vector3 localAngularVelo = transform.InverseTransformDirection(angularVelocity);
            float rollRate = localAngularVelo.z;
            float pitchRate = localAngularVelo.x;
            float yawRate = localAngularVelo.y;

            // 转为机体坐标
            Vector3 localAccel = transform.InverseTransformDirection(-commandAccel);

            // 映射到三轴输入（-1 到 1）
            float pitchCmd = Mathf.Clamp(localAccel.y / (maxG * 9.81f) - pitchRate * pitchPID.kd, -1f, 1f);
            float yawCmd = Mathf.Clamp(localAccel.x / (maxG * 9.81f) - yawRate * yawPID.kd, -1f, 1f);
            float rollCmd = CalculateRollStabilization();

            Vector3 result = new Vector3(yawCmd, pitchCmd, rollCmd);
            // 直接设置 controllingInput
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