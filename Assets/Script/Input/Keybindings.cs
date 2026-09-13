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
        public static KeyCode mslView = KeyCode.U;
        public static KeyCode toggleGear = KeyCode.G;

        public static KeyCode fireMain = KeyCode.Space;
        public static KeyCode shutdownSeeker = KeyCode.B;
        public static KeyCode[] radarHmdLock = new KeyCode[] { KeyCode.LeftAlt, KeyCode.F };
        public static KeyCode[] radarTwsLock = new KeyCode[] { KeyCode.LeftAlt, KeyCode.X };
        public static KeyCode[] radarNextMode = new KeyCode[] { KeyCode.LeftAlt, KeyCode.B };
        public static KeyCode[] countermeasuresNextMode = new KeyCode[] { KeyCode.LeftAlt, KeyCode.S };
        public static KeyCode[] countermeasuresDeploy = new KeyCode[] { KeyCode.LeftAlt, KeyCode.E };

        public static bool radarHmdLockDown = false;
        public static bool radarTwsLockDown = false;
        public static bool radarNextModeDown = false;
        public static bool toggleGearDown = false;
        public static bool shutdownSeekerDown = false;
        public static bool countermeasuresNextModeDown = false;
        public static bool countermeasuresDeployDown = false;

        public void Update()
        {
            ResetAllKeys();

            OnCombinedKeyDown(ref radarHmdLockDown, radarHmdLock);
            OnCombinedKeyDown(ref radarTwsLockDown, radarTwsLock);
            OnCombinedKeyDown(ref radarNextModeDown, radarNextMode);
            OnCombinedKeyDown(ref countermeasuresNextModeDown, countermeasuresNextMode);
            OnCombinedKeyDown(ref countermeasuresDeployDown, countermeasuresDeploy);

            UpdateSignleKeyDown(ref toggleGearDown, toggleGear);
            UpdateSignleKeyDown(ref shutdownSeekerDown, shutdownSeeker);
        }

        private void ResetAllKeys()
        {
            radarHmdLockDown = false;
            radarTwsLockDown = false;
            radarNextModeDown = false;
            toggleGearDown = false;
            shutdownSeekerDown = false;
            countermeasuresNextModeDown = false;
            countermeasuresDeployDown = false;
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

        private void OnCombinedKeyDown(ref bool keyDown, KeyCode[] keys)
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
            keyDown = anyKeyDown && allKeyPressing;
        }
    }
}