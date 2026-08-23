using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using UnityEngine;

namespace AeroSim.UI
{
    public class VirtualJoystickUI : MonoBehaviour
    {
        public float scale;
        public Transform center;
        public Transform cursor;
        public Transform yawCursor;

        public void Update()
        {
            Vector3 rawInput = Aircraft.main.ControllingInput;
            Vector3 input = new Vector2(rawInput.z, rawInput.y);
            cursor.position = center.position + input * scale;
            yawCursor.position = center.position + new Vector3(rawInput.x * scale, -scale, 0);
        }
    }
}