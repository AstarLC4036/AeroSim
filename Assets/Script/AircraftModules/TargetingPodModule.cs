using AeroSim.InputSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static AeroSim.Utility.MathUtility;

namespace AeroSim.AircraftModules
{
    public class TargetingPodModule : AircraftModule
    {
        [Header("Mechanical Parts")]
        public Transform azimuthGimbal;     // 方位转轴（水平旋转）
        public Transform elevationGimbal;   // 俯仰转轴（垂直旋转）
        public Transform rollGimbal;        // 滚转稳定轴（可选）
        public Transform podCameraGimbal;
        public Camera podCamera;         // 固定在红外头上的摄像机

        [Header("Gimbal Limits")]
        public float maxAzimuth = 80f;       // 最大方位角
        public float minElevation = -60f;    // 最小俯仰角
        public float maxElevation = 60f;     // 最大俯仰角

        [Header("Movement")]
        public float rotationSpeed = 30f;    // 机械旋转速度（度/秒）
        public float smoothing = 5f;         // 平滑系数

        [Header("Current State")]
        public float currentAzimuth = 0f;    // 当前方位角
        public float currentElevation = 0f;  // 当前俯仰角
        public float currentFov = 0f;  // 当前FOV

        [Header("Camera Control")]
        public float defaultFov = 40;
        public float zoomFov = 10;
        public float fovSmoothing = 5f;         // 平滑系数

        private Vector3 azimuthGimbalOffset;
        private Vector3 elevationGimbalOffset;
        private float targetAzimuth = 0f;
        private float targetElevation = 0f;
        private float targetFov;
        [SerializeField]
        private Vector3 targetDir = Vector3.forward;

        private void Start()
        {
            azimuthGimbalOffset = azimuthGimbal.localEulerAngles;
            elevationGimbalOffset = elevationGimbal.localEulerAngles;
        }

        void Update()
        {
            if(parentAircraft != null && parentAircraft.isControlling)
                HandleInput();

            ClampTargetAngles();

            UpdateState();
        }

        void HandleInput()
        {
            Vector3 mouseInput = Input.mousePositionDelta;

            //targetAzimuth += mouseInput.x * rotationSpeed;
            //targetElevation += mouseInput.y * rotationSpeed * -1;

            targetDir = RotateRound(targetDir, Vector3.zero, podCamera.transform.up, mouseInput.x * rotationSpeed * Time.deltaTime);
            targetDir = RotateRound(targetDir, Vector3.zero, podCamera.transform.right, -mouseInput.y * rotationSpeed * Time.deltaTime);

            targetDir = ClampTargetDir(targetDir);

            Vector3 dir = transform.InverseTransformDirection(targetDir);

            float rollAngle = CovertAngle(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90);
            float pitchAngle = Vector3.Angle(Vector3.forward, dir);
            targetAzimuth = -rollAngle;
            targetElevation = pitchAngle;

            if(Input.GetKey(Keybindings.focusCam) && targetFov != zoomFov)
            {
                targetFov = zoomFov;
            }
            else if (!Input.GetKey(Keybindings.focusCam) && targetFov != defaultFov)
            {
                targetFov = defaultFov;
            }
        }

        void ClampTargetAngles()
        {
            targetAzimuth = Mathf.Clamp(targetAzimuth, -maxAzimuth, maxAzimuth);
            targetElevation = Mathf.Clamp(targetElevation, minElevation, maxElevation);
        }

        void UpdateState()
        {
            // 平滑旋转（模拟机械惯性）
            currentAzimuth = Mathf.Lerp(currentAzimuth, targetAzimuth, smoothing * Time.deltaTime);
            currentElevation = Mathf.Lerp(currentElevation, targetElevation, smoothing * Time.deltaTime);
            // 平滑缩放
            currentFov = Mathf.Lerp(currentFov, targetFov, fovSmoothing * Time.deltaTime);

            // 应用旋转
            azimuthGimbal.localRotation = Quaternion.Euler(azimuthGimbalOffset + new Vector3(currentAzimuth, 0, 0));
            elevationGimbal.localRotation = Quaternion.Euler(elevationGimbalOffset + new Vector3(0, 0, currentElevation));

            podCamera.fieldOfView = currentFov;

            if (podCamera == null) return;

            // 获取当前观察方向
            Vector3 forward = podCamera.transform.forward;

            // 如果观察方向接近垂直，使用世界up会导致万向节问题，可加保护
            Vector3 up = Vector3.up;
            if (Mathf.Abs(forward.y) > 0.99f)
            {
                // 垂直时改用参考物体的up
                up = (elevationGimbal != null) ? elevationGimbal.up : Vector3.forward;
            }

            // 构造水平旋转（保持观察方向，但滚转归零）
            Quaternion levelRotation = Quaternion.LookRotation(forward, up);

            // 应用到相机
            podCamera.transform.rotation = levelRotation;
        }

        public Vector3 ClampTargetDir(Vector3 targetDir)
        {
            Vector3 dir = transform.InverseTransformDirection(targetDir);

            // 2. 计算方位角和俯仰角
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            // 3. 限制角度
            yaw = Mathf.Clamp(yaw, -maxAzimuth, maxAzimuth);
            pitch = Mathf.Clamp(-pitch, minElevation, maxElevation);

            // 4. 根据限制后的角度重构方向
            Vector3 clampedLocalDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

            // 5. 转回世界坐标
            return transform.TransformDirection(clampedLocalDir);
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(elevationGimbal.position, elevationGimbal.position + targetDir.normalized * 3);
            Gizmos.DrawLine(elevationGimbal.position, elevationGimbal.position + transform.forward * 3);
        }
    }
}
