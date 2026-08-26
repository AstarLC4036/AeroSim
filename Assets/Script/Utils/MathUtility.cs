using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim.Utility
{
    public class MathUtility
    {
        /// <summary>
        /// Floor to custom decimal places
        /// </summary>
        public static float FloorToDC(float num, int decimalPlaces)
        {
            return Mathf.FloorToInt(num * Mathf.Pow(10, decimalPlaces)) / Mathf.Pow(10, decimalPlaces);
        }

        public static float TransformAngle(float angle, float fov, float pixelHeight)
        {
            return (Mathf.Tan(angle * Mathf.Deg2Rad) / Mathf.Tan(fov / 2 * Mathf.Deg2Rad)) * pixelHeight / 2;
        }

        public static float CovertAngle(float angle)
        {
            if(angle > 180)
            {
                angle -= 360;
            }
            if(angle < -180)
            {
                angle += 360;
            }

            return angle;
        }

        public static float CovertAngle360(float angle)
        {
            if(angle < 0)
            {
                angle = 360 + angle;
            }

            return angle;
        }

        public static Vector3 RotateRound(Vector3 position, Vector3 center, Vector3 axis, float angle)
        {
            return Quaternion.AngleAxis(angle, axis) * (position - center) + center;
        }

        public static bool ConeDetect(Vector3 position, Vector3 forward, Vector3 targetPos, float fov)
        {
            Vector3 targetDir = targetPos - position;
            float angle = Vector3.Angle(targetDir, forward);
            float z = Vector3.Dot(targetDir, forward);
            return angle <= fov && z >= 0;
        }

        public static Vector3 ClampTargetDir(Vector3 localTargetDir, float minYaw, float maxYaw, float minPitch, float maxPitch)
        {
            float yaw = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(localTargetDir.y, -1f, 1f)) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
            pitch = Mathf.Clamp(-pitch, minPitch, maxPitch);

            Vector3 clampedLocalDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

            return clampedLocalDir;
        }

        public static Vector3 ClampTargetDir(Transform transform, Vector3 targetDir, float minYaw, float maxYaw, float minPitch, float maxPitch)
        {
            return transform.TransformDirection(ClampTargetDir(transform.InverseTransformDirection(targetDir), minYaw, maxYaw, minPitch, maxPitch));
        }

        /// <summary>
        /// 在方向向量上叠加角度抖动
        /// </summary>
        /// <param name="direction">原始方向（应归一化）</param>
        /// <param name="amplitudeDeg">抖动幅度（度）</param>
        /// <param name="frequency">抖动频率</param>
        /// <param name="up">参考上方向（用于构造正交基）</param>
        /// <returns>抖动后的方向（未归一化）</returns>
        public static Vector3 ApplyAngularJitter(Vector3 direction, float amplitudeDeg, float frequency, Vector3 up)
        {
            direction = direction.normalized;
            up = Vector3.ProjectOnPlane(up, direction).normalized;
            Vector3 right = Vector3.Cross(direction, up).normalized;

            // 生成两个正交方向的 Perlin 噪声
            float noisePitch = (Mathf.PerlinNoise(Time.time * frequency, 0f) - 0.5f) * amplitudeDeg;
            float noiseYaw = (Mathf.PerlinNoise(0f, Time.time * frequency) - 0.5f) * amplitudeDeg;

            // 绕 up 轴旋转（偏航），绕 right 轴旋转（俯仰）
            Quaternion yawRot = Quaternion.AngleAxis(noiseYaw, up);
            Quaternion pitchRot = Quaternion.AngleAxis(noisePitch, right);

            return (yawRot * pitchRot) * direction;
        }
    }
}
