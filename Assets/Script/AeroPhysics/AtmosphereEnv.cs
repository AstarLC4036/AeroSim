using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AeroSim.AeroPhysics
{
    public class AtmosphereEnv
    {
        public const float T0 = 15.04f + 273.15f; // Sea Level Temperature
        public const float P0 = 101325; // 101.325 kPa - Sea Level Pressure
        public const float R = 287.053f; // J/(kg * K)
        public const float L = 0.0065f; // K/m
        public const float gamma = 1.4f;
        public const float g = 9.8f; // m/(s^2)

        public const float H11 = 11000f;   // 对流层顶
        public const float H25 = 25000f;   // 等温层顶
        public const float T11 = 216.65f;  // 11~25 km 等温层温度 (K)
        public const float P11 = 22632f;   // 11 km 处压力 (Pa)
        public const float L2 = 0.00299f;  // 25 km 以上升温率 (K/m)

        public static float Temperature(float altitude)
        {
            float temperature;
            if (altitude < 11000)
            {
                temperature = 15.04f - 0.00649f * altitude;
            }
            else if (altitude < 25000)
            {
                temperature = -56.46f; // 216.65K
            }
            else
            {
                temperature = -131.21f + 0.00299f * altitude;
            }

            return temperature;
        }

        public static float TemperatureK(float altitude)
        {
            float temperature;
            if (altitude < 11000)
            {
                temperature = T0 - 0.00649f * altitude;
            }
            else if (altitude < 25000)
            {
                temperature = 216.65f;
            }
            else
            {
                temperature = 141.94f + 0.00299f * altitude;
            }

            return temperature;
        }

        public static float Pressure(float altitude)
        {
            if (altitude < H11)
                return P0 * Mathf.Pow(1f - L * altitude / T0, g / (R * L));

            if (altitude < H25)
                return P11 * Mathf.Exp(-g * (altitude - H11) / (R * T11));

            // 25 km 以上（升温段）：先算出 25 km 处的基准压力，再用幂律。
            // 注意：这里千万别写 Pressure(H25) —— 它同样会落进本分支，造成无限递归，
            // StackOverflowException 会直接把 Unity 进程干掉（编辑器崩溃、没有崩溃转储）。
            float p25 = P11 * Mathf.Exp(-g * (H25 - H11) / (R * T11));
            return p25 * Mathf.Pow(T11 / (T11 + L2 * (altitude - H25)), g / (R * L2));
        }

        public static float Density(float altitude)
        {
            return Pressure(altitude) / (R * TemperatureK(altitude));
        }

        public static float SonicSpeed(float altitude)
        {
            return Mathf.Sqrt(gamma * R * TemperatureK(altitude));
        }
    }
}