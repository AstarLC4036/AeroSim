using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Cockpit
{
    public class ThrottleMapper : MonoBehaviour
    {
        public Aircraft aircraft;
        public float maxAngle;

        private EngineModule engine;

        public void Start()
        {
            engine =  aircraft.engine;
        }

        public void FixedUpdate()
        {
            if(engine == null)
            {
                engine = aircraft.engine;
            }
            // The 3D throttle lever follows the lever position directly (the old 1.1 multiplier turned the grip past the printed markings)
            float percent = engine.ThrottleLever;
            transform.localEulerAngles = new Vector3(maxAngle * percent, transform.localEulerAngles.y, transform.localEulerAngles.z);
        }
    }
}
