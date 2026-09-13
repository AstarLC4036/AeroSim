using AeroSim.AeroPhysics;
using AeroSim.General;
using AeroSim.InputSystem;
using System;
using System.Collections;
using UnityEngine;
using static AeroSim.AircraftModules.RadarModule;

namespace AeroSim.AircraftModules
{
    public class CountermeasureModule : AircraftModule
    {
        public enum EjectMode
        {
            Flare = 0,
            Chaff = 1,
            All = 2
        }

        public EjectMode ejectMode = EjectMode.All;
        public int countermeasureCapacity = 108;
        public int flareCount = 36;
        public int chaffCount = 72;
        public Transform[] ejectPoints = new Transform[0];
        public float ejectForce = 1000;
        public int ejectPositionIndex = 0;

        public void Update()
        {
            if (parentAircraft.isControlling)
            {
                if (Keybindings.countermeasuresNextModeDown)
                {
                    ejectMode = (EjectMode)(((int)ejectMode + 1) % Enum.GetValues(typeof(EjectMode)).Length);
                    VehicleLog.LogMsg($"干扰物模式 {ParseCountermeasureMode()}");
                }

                if (Keybindings.countermeasuresDeployDown)
                {
                    switch (ejectMode)
                    {
                        case EjectMode.Flare:
                            DeployFlare();
                            break;
                        case EjectMode.Chaff:
                            DeployChaff();
                            break;
                        case EjectMode.All:
                            DeployAll();
                            break;
                    }
                }
            }
        }

        private string ParseCountermeasureMode()
        {
            switch (ejectMode)
            {
                case EjectMode.Flare:
                    return "热诱";
                case EjectMode.Chaff:
                    return "箔条";
                case EjectMode.All:
                    return "热诱+箔条";
                default:
                    return "Unknown";
            }
        }

        public void DeployAll()
        {
            DeployFlare();
            DeployChaff();
        }

        public void DeployFlare()
        {
            if (flareCount > 0)
            {
                flareCount--;
                FlareCm flare = CountermeasurePool.Instance.flarePool.Get();
                flare.transform.position = ejectPoints[ejectPositionIndex].position;
                flare.rb.AddForce(ejectPoints[ejectPositionIndex].forward * ejectForce);
                NextPoint();
            }
        }

        public void DeployChaff()
        {
            if (chaffCount > 0)
            {
                chaffCount--;
                //todo
            }
        }

        private void NextPoint()
        {
            ejectPositionIndex++;
            if (ejectPositionIndex >= ejectPoints.Length)
                ejectPositionIndex = 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (ejectPoints.Length > 0)
            {
                Gizmos.color = Color.yellow;
                foreach (Transform point in ejectPoints)
                {
                    Gizmos.DrawSphere(point.position, 0.1f);
                    Gizmos.DrawLine(point.position, point.position + point.forward);
                }
            }
        }
    }
}