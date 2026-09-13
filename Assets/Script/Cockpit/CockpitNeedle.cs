using System.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace AeroSim.Cockpit
{
    public class CockpitNeedle : CockpitIndicator
    {
        public CockpitDataManager.CockpitDataType dataType;
        public Vector2 inputRange;
        public Vector2 angleRange;
        public Axis rotateAxis;

        protected override void UpdateIndicator()
        {
            float value = Mathf.Clamp(CockpitDataManager.GetDataFloat(dataType), inputRange.x, inputRange.y);
            float angleLerpPercent = (value - inputRange.x) / (inputRange.y - inputRange.x);
            float targetAngle = Mathf.Lerp(angleRange.x, angleRange.y, angleLerpPercent);
            transform.localEulerAngles = new Vector3(
                rotateAxis == Axis.X ? targetAngle : transform.localEulerAngles.x,
                rotateAxis == Axis.Y ? targetAngle : transform.localEulerAngles.y,
                rotateAxis == Axis.Z ? targetAngle : transform.localEulerAngles.z
            );
        }
    }
}