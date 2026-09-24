using AeroSim.InputSystem;
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
            }
            else if(landingGear.time == landingGear.duration)
            {
                landingGear.Play(-1);
            }
            else
            {
                landingGear.speed *= -1;
            }
        }
    }
}