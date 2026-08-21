using UnityEngine;

namespace AeroSim.Util
{
    public struct BiVector3
    {
        public Vector3 lift;
        public Vector3 drag;
        public Vector3 torque;

        public BiVector3(Vector3 lift, Vector3 drag)
        {
            this.lift = lift;
            this.drag = drag;
            torque = Vector3.zero;
        }

        public BiVector3(Vector3 lift, Vector3 drag, Vector3 torque)
        {
            this.lift = lift;
            this.drag = drag;
            this.torque = torque;
        }

        public static BiVector3 operator +(BiVector3 left, BiVector3 right)
        {
            BiVector3 result = left;
            result.lift += right.lift;
            result.drag += right.drag;
            result.torque += right.torque;
            return result;
        }
    }
}
