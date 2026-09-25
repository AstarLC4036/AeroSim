using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace AeroSim.Cockpit
{
    public class CockpitNeedle : CockpitIndicator
    {
        [Serializable]
        public class RangeMapping
        {
            public Vector2 inputRange;
            public Vector2 angleRange;
        }

        public CockpitDataManager.CockpitDataType dataType;
        public RangeMapping[] ranges;
        public Axis rotateAxis;
        public bool invert = false;

        protected override void UpdateIndicator()
        {
            float value = CockpitDataManager.GetDataFloat(dataType);
            if (ranges != null && ranges.Length > 0)
            {
                foreach (RangeMapping rng in ranges)
                {
                    if (!(value >= rng.inputRange.x && value <= rng.inputRange.y))
                    {
                        continue;
                    }

                    value = Mathf.Clamp(value, rng.inputRange.x, rng.inputRange.y);
                    float angleLerpPercent = (value - rng.inputRange.x) / (rng.inputRange.y - rng.inputRange.x);
                    float targetAngle = Mathf.Lerp(rng.angleRange.x, rng.angleRange.y, angleLerpPercent);
                    targetAngle = invert ? -targetAngle : targetAngle;
                    transform.localEulerAngles = new Vector3(
                        rotateAxis == Axis.X ? targetAngle : transform.localEulerAngles.x,
                        rotateAxis == Axis.Y ? targetAngle : transform.localEulerAngles.y,
                        rotateAxis == Axis.Z ? targetAngle : transform.localEulerAngles.z
                    );
                }
            }
            else
            {
                float targetAngle = invert ? -value : value;
                transform.localEulerAngles = new Vector3(
                    rotateAxis == Axis.X ? targetAngle : transform.localEulerAngles.x,
                    rotateAxis == Axis.Y ? targetAngle : transform.localEulerAngles.y,
                    rotateAxis == Axis.Z ? targetAngle : transform.localEulerAngles.z
                );
            }
         }
    }
}