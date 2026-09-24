using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace AeroSim.AircraftModules
{
    [ExecuteInEditMode]
    public class HydraulicMechSystem : MonoBehaviour
    {
        [Serializable]
        public class MechMotion
        {
            public MechMotionType motionType;
            public Transform attachedTransform;
            public Vector3 startValue;
            public Vector3 endValue;
            public AnimationCurve mechAnimCurve;
        }

        public enum MechMotionType
        {
            Linear,
            Rotational
        }

        [Header("Mech Data")]
        public List<MechMotion> mechMotions = new List<MechMotion>();

        [Header("Hydraulic Property")]
        public float duration;
        public float time;
        public float speed;

        public bool showPreview;

        public void Play(int speed = 1)
        {
            this.speed = speed;
        }

        public void Stop()
        {
            speed = 0;
        }

        public void ResetState(float time = 0)
        {
            this.time = time;
            speed = 0;
            ApplyMotion(time);
        }

        #if UNITY_EDITOR
        void Update()
        {
            if(!Application.isPlaying && showPreview)
            {
                if ((speed < 0 && time > 0) || (speed > 0 && time < duration))
                {
                    time += speed * Time.deltaTime;
                    time = Mathf.Clamp(time, 0, duration);

                    ApplyMotion(time);
                }
            }
        }
        #endif

        public void FixedUpdate()
        {
            if ((speed < 0 && time > 0) || (speed > 0 && time < duration))
            {
                time += speed * Time.fixedDeltaTime;
                time = Mathf.Clamp(time, 0, duration);

                if (time == 0 && speed < 0)
                    speed = 0;
                else if (time == duration && speed > 0)
                    speed = 0;

                ApplyMotion(time);
            }
        }

        public void ApplyMotion(float time)
        {
            foreach (var motion in mechMotions)
            {
                float t = motion.mechAnimCurve.Evaluate(time);
                Vector3 newValue = Vector3.Lerp(motion.startValue, motion.endValue, t);
                switch (motion.motionType)
                {
                    case MechMotionType.Linear:
                        motion.attachedTransform.localPosition = newValue;
                        break;
                    case MechMotionType.Rotational:
                        motion.attachedTransform.localRotation = Quaternion.Euler(newValue);
                        break;
                }
            }
        }
    }
}