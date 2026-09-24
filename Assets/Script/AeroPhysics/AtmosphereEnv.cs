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
            float pressure = 0;
            if(altitude < 11000)
            {
                pressure = P0 * Mathf.Pow((1 - L * altitude / T0), g / (R * L));
            }
            else if (altitude < 25000)
            {
                pressure = 22632 * Mathf.Exp(-g * (altitude - 11000) / (R * 216.65f));
                // 26623 ~= P0 * (1 - L * 11000 / T0)^g / (R * L)
            }
            else
            {
                // 升温段，L = +0.00299 K/m：用幂律
                const float L2 = 0.00299f;
                float T25 = 141.94f + L2 * 25000f;          // 216.69 K
                pressure = Pressure(25000) *
                           Mathf.Pow(T25 / (T25 + L2 * (altitude - 25000f)), g / (R * L2));
            }

            return pressure;
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