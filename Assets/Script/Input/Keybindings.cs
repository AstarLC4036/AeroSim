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

        // Smoothed mouse movement, shared by everything that keeps steering while the mouse moves (aim
        // ring, camera, targeting pod). The raw per frame delta is noisy and its size depends on the
        // frame rate, and both go straight into whatever control loop consumes it.
        //   smoothMouseDelta - pixels this frame  -> integrate it (angle per pixel), never divide by dt
        //   smoothMouseRate  - pixels per second  -> thresholds, magnitude checks, and anything that
        //                                            needs a frame rate independent measure
        public static float mouseSmoothing = 0.05f;        // seconds, 0 or less disables the filter
        public static float mouseSmoothingMaxStep = 0.05f; // seconds: upper bound for the filter's step
        public static float mouseDeltaLimit = 0f;          // pixels per frame, 0 or less means no limit
        public static Vector2 smoothMouseDelta = Vector2.zero;
        public static Vector2 smoothMouseRate = Vector2.zero;   // pixels per second

        public void Update()
        {
            ResetAllKeys();
            UpdateSmoothMouseDelta();

            OnCombinedKeyDown(ref radarHmdLockDown, radarHmdLock);
            OnCombinedKeyDown(ref radarTwsLockDown, radarTwsLock);
            OnCombinedKeyDown(ref radarNextModeDown, radarNextMode);
            OnCombinedKeyDown(ref countermeasuresNextModeDown, countermeasuresNextMode);
            OnCombinedKeyDown(ref countermeasuresDeployDown, countermeasuresDeploy);

            UpdateSignleKeyDown(ref toggleGearDown, toggleGear);
            UpdateSignleKeyDown(ref shutdownSeekerDown, shutdownSeeker);
        }

        /// <summary>
        /// First order lag on the mouse delta, in pixels. Using Exp() keeps the response independent of
        /// the frame rate, which a fixed per frame blend factor would not be.
        /// <para>The step fed to the filter is capped. Without the cap a slow frame makes the blend
        /// approach 1, so the filter stops smoothing exactly when it is needed most and the whole frame's
        /// movement lands in one step - which reads as a sudden acceleration when the frame rate drops.
        /// The cap keeps a minimum amount of smoothing on slow frames instead.</para>
        /// </summary>
        private void UpdateSmoothMouseDelta()
        {
            Vector2 raw = Input.mousePositionDelta;
            float dt = Time.unscaledDeltaTime;

            if (mouseDeltaLimit > 0f && raw.sqrMagnitude > mouseDeltaLimit * mouseDeltaLimit)
            {
                raw = raw.normalized * mouseDeltaLimit;
            }

            if (mouseSmoothing <= 0f)
            {
                smoothMouseDelta = raw;
            }
            else
            {
                float step = Mathf.Min(dt, Mathf.Max(mouseSmoothingMaxStep, mouseSmoothing));
                float blend = 1f - Mathf.Exp(-step / mouseSmoothing);
                smoothMouseDelta = Vector2.Lerp(smoothMouseDelta, raw, blend);
            }

            smoothMouseRate = dt > 1e-5f ? smoothMouseDelta / dt : Vector2.zero;
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