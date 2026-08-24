using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using UnityEngine;

namespace AeroSim.UI
{
    public class VirtualJoystickUI : MonoBehaviour
    {
        public float scaleInner;
        public float scaleExternalX;
        public float scaleExternalY;
        public Transform cursor;
        public Transform yawCursor;
        public Transform throttleCursor;
        private Vector3 centerOffset;
        private Vector3 yawOffset;
        private Vector3 throttleOffset;

        private void Start()
        {
            centerOffset = cursor.position;
            yawOffset = yawCursor.position;
            throttleOffset = throttleCursor.position;
        }

        public void Update()
        {
            Vector3 rawInput = Aircraft.main.ControllingInput;
            float throttle = Aircraft.main.engine.thurst / Aircraft.main.engine.maxThurst;
            Vector3 input = new Vector2(rawInput.z, rawInput.y);
            cursor.position = centerOffset + input * scaleInner;
            yawCursor.position = yawOffset + new Vector3(rawInput.x * scaleExternalX, 0, 0);
            throttleCursor.position = throttleOffset + new Vector3(0, throttle * scaleExternalY, 0);
        }
    }
}