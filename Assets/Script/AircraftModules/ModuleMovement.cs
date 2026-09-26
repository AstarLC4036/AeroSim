using AeroSim.General;
using AeroSim.InputSystem;
using AeroSim.UI;
using System.Collections;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class ModuleMovement : AircraftModule
    {
        public HydraulicMechSystem landingGear;

        private void Start()
        {
            landingGear.ResetState(landingGear.duration);
        }

        private void Update()
        {
            if (Keybindings.toggleGearDown)
            {
                ToggleGear();
            }
        }

        public void ToggleGear()
        {
            if (landingGear.time == 0)
            {
                landingGear.Play();
                VehicleLog.LogMsg("起落架 > 放下");
            }
            else if(landingGear.time == landingGear.duration)
            {
                landingGear.Play(-1);
                VehicleLog.LogMsg("起落架 > 收起");
            }
            else
            {
                landingGear.speed *= -1;
            }
        }
    }
}