using AeroSim.AeroPhysics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public abstract class AircraftModule : MonoBehaviour
    {
        public string moduleName;

        protected Aircraft parentAircraft;

        public virtual void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;
        }
    }
}
