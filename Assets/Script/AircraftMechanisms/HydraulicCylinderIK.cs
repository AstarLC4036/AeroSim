using System.Collections;
using UnityEngine;

namespace AeroSim.AircraftMechanisms
{
    [ExecuteInEditMode]
    public class HydraulicCylinderIK : MonoBehaviour
    {
        [Header("液压杆结构")]
        public Transform fixedEnd;        // 固定端（缸体铰接点）
        public Transform cylinderBody;    // 外筒（绕固定端旋转）
        public Transform pistonRod;       // 活塞杆（沿外筒轴向伸缩）
        //public Transform rodTip;          // 杆头（活塞杆末端，可选）

        //[Header("尺寸")]
        //public float cylinderLength = 0.8f;   // 外筒长度（固定端到活塞杆起始）
        //public float minPistonLength = 0.1f;  // 活塞杆最短伸出量
        //public float maxPistonLength = 1.2f;  // 活塞杆最长伸出量

        public Transform target;
        public float pistonLength;
        public Vector3 cylinderAxis = Vector3.forward;  // 外筒轴向（通常为Z轴）

        void Update()
        {
            SolveHydraulic();
        }

        void SolveHydraulic()
        {
            if (fixedEnd == null || cylinderBody == null || pistonRod == null || target == null)
                return;

            // 1. 计算固定端到目标的方向和距离
            Vector3 toTarget = target.position - fixedEnd.position;
            //float distance = toTarget.magnitude;

            // 2. 外筒旋转指向目标
            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            cylinderBody.rotation = targetRotation;

            // 3. 活塞杆伸缩长度 = 距离 - 外筒长度
            //float pistonLength = Mathf.Clamp(distance - cylinderLength, minPistonLength, maxPistonLength);
            pistonRod.position = target.position;
            pistonRod.rotation = cylinderBody.rotation;

            // 4. 设置活塞杆沿轴向的本地位置
            //pistonRod.localPosition = cylinderAxis.normalized * pistonLength;

            //// 5. 可选：更新杆头位置（如果杆头是独立物体）
            //if (rodTip != null)
            //{
            //    rodTip.position = cylinderBody.TransformPoint(cylinderAxis.normalized * (cylinderLength + pistonLength));
            //}
        }

        void OnDrawGizmosSelected()
        {
            if (fixedEnd != null && target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(fixedEnd.position, target.position);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(target.position, 0.05f);
            }
        }
    }
}