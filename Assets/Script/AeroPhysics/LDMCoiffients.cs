using UnityEngine;

namespace AeroSim.AeroPhysics
{
    /// <summary>
    /// A struct used to store coefficient data
    /// </summary>
    public struct LDMCoefficients
    {
        public float liftCoefficient;
        public float dragCoefficient;
        public float torqueCoefficient;

        public LDMCoefficients(float liftCoefficient, float dragCoefficient, float torqueCoefficient)
        {
            this.liftCoefficient = liftCoefficient;
            this.dragCoefficient = dragCoefficient;
            this.torqueCoefficient = torqueCoefficient;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(liftCoefficient, dragCoefficient, torqueCoefficient);
        }

        public static LDMCoefficients FromVector3(float l, float d, float m)
        {
            return new LDMCoefficients(l, d, m);
        }

        public static LDMCoefficients FromVector3(Vector3 coef)
        {
            return new LDMCoefficients(coef.x, coef.y, coef.z);
        }
    }
}
