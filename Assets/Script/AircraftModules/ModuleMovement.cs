using AeroSim.InputSystem;
using System.Collections;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class ModuleMovement : AircraftModule
    {
        public Animator landingGear;
        private bool isGearDeployed;

        private void Start()
        {
            isGearDeployed = landingGear.GetBool("deployed");
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
            if (!isGearDeployed)
                DeployGear();
            else
                StowGear();
        }

        public void DeployGear()
        {
            landingGear.SetBool("deployed", true);
            isGearDeployed = true;
        }

        public void StowGear()
        {
            landingGear.SetBool("deployed", false);
            isGearDeployed = false;
        }
    }
}