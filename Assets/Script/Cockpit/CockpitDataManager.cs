using AeroSim.AeroPhysics;
using AeroSim.Utils;
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

        public enum WeaponState
        {
            NoWeapon,
            WeaponSafe,
            WeaponReay,
            Lock,
            InRange,
            Shoot
        }

        public Aircraft playerAircraft;
        public WeaponState weaponState;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            playerAircraft = Aircraft.main;
        }

        public static string GetParsedWeaponState()
        {
            return ParseWeaponState(Instance.weaponState);
        }

        public static string ParseWeaponState(WeaponState state)
        {
            switch(state)
            {
                case WeaponState.NoWeapon:
                    return "NO WPN";
                case WeaponState.WeaponSafe:
                    return "WPN SAFE";
                case WeaponState.WeaponReay:
                    return "WPN RDY";
                case WeaponState.Lock:
                    return "LOCK";
                case WeaponState.InRange:
                    return "IN RNG";
                case WeaponState.Shoot:
                    return "SHOOT";
                default:
                    return "NONE";
            }
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
                case CockpitDataType.Heading:
                    return playerAircraft.transform.eulerAngles.y;
                case CockpitDataType.Airspeed:
                    return playerAircraft.Velocity.magnitude;
                case CockpitDataType.Altitude:
                    return playerAircraft.transform.position.y - OriginKeeper.origin.y;
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
