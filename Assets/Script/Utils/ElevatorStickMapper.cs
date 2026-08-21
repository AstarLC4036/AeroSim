using System.Collections;
using UnityEngine;
using AeroSim.AeroPhysics;

namespace AeroSim.Utils
{
    public class ElevatorStickMapper : MonoBehaviour
    {
        public Aircraft aircraft;
        public Vector2 maxAngle;
        public bool invertRoll = false, invertPitch = false;

        // Update is called once per frame
        void Update()
        {
            Vector3 controlInput = aircraft.ControllingInput;

            transform.localEulerAngles = new Vector3(controlInput.y * maxAngle.y * (invertPitch ? -1 : 1), transform.localEulerAngles.y, controlInput.z * maxAngle.x * (invertRoll ? -1 : 1));
        }
    }
}