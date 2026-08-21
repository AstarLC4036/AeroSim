using UnityEngine;

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
            return angle <= fov;
        }
    }
}
