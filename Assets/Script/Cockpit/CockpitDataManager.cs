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

        public static Action<InteractionEvent> onInteracion = (type) => { };

        public enum CockpitDataType
        {
            Altitude,
            Airspeed,
            Throttle,
            Heading,
            VerticalSpeed,
            MachSpeed,
            FuelLevel,
            EngineRPM_N1,
            EngineRPM_N2,
            OilPressure,
            OilTemperature,
            CabinPressure,
            CabinTemperature,
            // Appended at the end on purpose: inserting in the middle shifts the integers serialised in
            // prefabs and silently re-points every existing CockpitNeedle. (EngineRPM_N2 was inserted in
            // the middle earlier, which moved OilPressure..CabinTemperature one slot down.)
            T4
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

        public enum InteractionEvent
        {
            StartEngine = 0
            // TODO: Other types
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
                        return playerAircraft.engine.ThrottleLever;
                    else
                        return 0;
                case CockpitDataType.MachSpeed:
                    if(playerAircraft.Rb != null)
                        return playerAircraft.Rb.velocity.magnitude /
                            Mathf.Max(AtmosphereEnv.SonicSpeed(playerAircraft.transform.position.y - FloatingOrigin.origin.y), 1f);
                    else
                        return 0;
                case CockpitDataType.Heading:
                    return playerAircraft.transform.eulerAngles.y;
                case CockpitDataType.Airspeed:
                    return playerAircraft.Velocity.magnitude;
                case CockpitDataType.Altitude:
                    return playerAircraft.transform.position.y - FloatingOrigin.origin.y;
                case CockpitDataType.EngineRPM_N1:
                    // N1 spool speed, normalised 0 -1
                    if(playerAircraft.engine != null)
                        return playerAircraft.engine.Rpm1;
                    else
                        return 0;
                case CockpitDataType.EngineRPM_N2:
                    // N2 spool speed
                    if(playerAircraft.engine != null)
                        return playerAircraft.engine.Rpm2;
                    else
                        return 0;
                case CockpitDataType.T4:
                    // Turbine-exit temperature in deg C (unlike N1/N2 this one is not normalised, because a
                    // temperature gauge is read in degrees)
                    if(playerAircraft.engine != null)
                        return playerAircraft.engine.T4;
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
