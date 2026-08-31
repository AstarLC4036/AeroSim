using UnityEngine;

namespace AeroSim.InputSystem
{
    public class Keybindings : MonoBehaviour
    {
        public static KeyCode holdControlInput = KeyCode.C;
        public static KeyCode thurstUp = KeyCode.LeftShift;
        public static KeyCode thurstDown = KeyCode.LeftControl;
        public static KeyCode focusCam = KeyCode.Z;
        public static KeyCode changeView = KeyCode.V;
        public static KeyCode mslView = KeyCode.P;
        public static KeyCode toggleGear = KeyCode.G;

        public static KeyCode fireMain = KeyCode.Space;
        public static KeyCode[] radarHmdLock = new KeyCode[] { KeyCode.LeftAlt, KeyCode.F };
        public static KeyCode[] radarTwsLock = new KeyCode[] { KeyCode.LeftAlt, KeyCode.X };
        public static KeyCode[] radarNextMode = new KeyCode[] { KeyCode.LeftAlt, KeyCode.B };

        public static bool radarHmdLockDown = false;
        public static bool radarTwsLockDown = false;
        public static bool radarNextModeDown = false;
        public static bool toggleGearDown = false;

        public void Update()
        {
            ResetAllKeys();

            if(OnCombinedKeyDown(radarHmdLock))
            {
                radarHmdLockDown = true;
            }

            if(OnCombinedKeyDown(radarTwsLock))
            {
                radarTwsLockDown = true;
            }

            if(OnCombinedKeyDown(radarNextMode))
            {
                radarNextModeDown = true;
            }

            UpdateSignleKeyDown(ref toggleGearDown, toggleGear);
        }

        private void ResetAllKeys()
        {
            radarHmdLockDown = false;
            radarTwsLockDown = false;
            radarNextModeDown = false;
        }

        private void UpdateSignleKeyDown(ref bool keyDown, KeyCode keyCode)
        {
            if (Input.GetKeyDown(keyCode) && !keyDown)
            {
                keyDown = true;
            }
            else if (keyDown && !Input.GetKeyDown(keyCode))
            {
                keyDown = false;
            }
        }

        private bool OnCombinedKeyDown(KeyCode[] keys)
        {
            bool allKeyPressing = true;
            bool anyKeyDown = false;
            foreach(KeyCode key in keys)
            {
                if(Input.GetKeyDown(key))
                {
                    anyKeyDown = true;
                }

                if(!(Input.GetKeyDown(key) || Input.GetKey(key)) && allKeyPressing)
                {
                    allKeyPressing = false;
                }
            }
            return anyKeyDown && allKeyPressing;
        }
    }
}