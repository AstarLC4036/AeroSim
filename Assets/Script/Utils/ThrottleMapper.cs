using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Utils
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
            float percent = engine.thurst / engine.maxThurst * 1.1f;
            transform.localEulerAngles = new Vector3(maxAngle * percent, transform.localEulerAngles.y, transform.localEulerAngles.z);
        }
    }
}
