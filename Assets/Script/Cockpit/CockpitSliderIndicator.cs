using System.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace AeroSim.Cockpit
{
    public class CockpitSliderIndicator : CockpitIndicator
    {
        public CockpitDataManager.CockpitDataType dataType;
        public Vector2 inputRange;
        public Vector2 slideRange;
        public Axis rotateAxis;

        protected override void UpdateIndicator()
        {
            float value = Mathf.Clamp(CockpitDataManager.GetDataFloat(dataType), inputRange.x, inputRange.y);
            float posPercent = (value - inputRange.x) / (inputRange.y - inputRange.x);
            float targetPos = Mathf.Lerp(slideRange.x, slideRange.y, posPercent);
            transform.localPosition = new Vector3(
                rotateAxis == Axis.X ? targetPos : transform.localPosition.x,
                rotateAxis == Axis.Y ? targetPos : transform.localPosition.y,
                rotateAxis == Axis.Z ? targetPos : transform.localPosition.z
            );
        }
    }
}