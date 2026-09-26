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
            // 三 D 油门杆直接跟杆位走（原来乘 1.1 会让手柄转过印字刻度）
            float percent = engine.ThrottleLever;
            transform.localEulerAngles = new Vector3(maxAngle * percent, transform.localEulerAngles.y, transform.localEulerAngles.z);
        }
    }
}
