using AeroSim.AeroPhysics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Cockpit
{
    public class CockpitDataManager : MonoBehaviour
    {
        private static CockpitDataManager instance;
        public static CockpitDataManager Instance => instance;

        public enum CockpitDataType
        {
            Altitude,
            Airspeed,
            Throttle,
            Heading,
            VerticalSpeed,
            MachSpeed,
            FuelLevel,
            EngineRPM,
            OilPressure,
            OilTemperature,
            CabinPressure,
            CabinTemperature
        }

        public Aircraft playerAircraft;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            playerAircraft = Aircraft.main;
        }

        public float GetFloatData(CockpitDataType dataType)
        {
            switch (dataType)
            {
                case CockpitDataType.Throttle:
                    if(playerAircraft.engine != null)
                        return playerAircraft.engine.thurst / playerAircraft.engine.maxThurst;
                    else
                        return 0;
                case CockpitDataType.MachSpeed:
                    if(playerAircraft.Rb != null)
                        return playerAircraft.Rb.velocity.magnitude / 340.3f;
                    else
                        return 0;
                default:
                    return 0;
            }
        }

        public static float GetDataFloat(CockpitDataType dataType)
        {
            return instance.GetFloatData(dataType);
        }
    }
}
