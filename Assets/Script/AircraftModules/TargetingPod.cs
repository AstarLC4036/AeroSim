using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class TargetingPod : AircraftModule
    {
        [Header("Mechanical Parts")]
        public Transform azimuthGimbal;     // 方位转轴（水平旋转）
        public Transform elevationGimbal;   // 俯仰转轴（垂直旋转）
        public Transform rollGimbal;        // 滚转稳定轴（可选）
        public Transform podCameraGimbal;
        public Transform podCamera;         // 固定在红外头上的摄像机

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

        private Vector3 azimuthGimbalOffset;
        private Vector3 elevationGimbalOffset;
        private float targetAzimuth = 0f;
        private float targetElevation = 0f;

        private void Start()
        {
            azimuthGimbalOffset = azimuthGimbal.localEulerAngles;
            elevationGimbalOffset = elevationGimbal.localEulerAngles;
        }

        void Update()
        {
            HandleInput();

            ClampTargetAngles();

            RotateGimbals();
        }

        void HandleInput()
        {
            Vector3 mouseInput = Input.mousePositionDelta;

            targetAzimuth += mouseInput.x * rotationSpeed;
            targetElevation += mouseInput.y * rotationSpeed * -1;
        }

        void ClampTargetAngles()
        {
            targetAzimuth = Mathf.Clamp(targetAzimuth, -maxAzimuth, maxAzimuth);
            targetElevation = Mathf.Clamp(targetElevation, minElevation, maxElevation);
        }

        void RotateGimbals()
        {
            // 平滑旋转（模拟机械惯性）
            currentAzimuth = Mathf.Lerp(currentAzimuth, targetAzimuth, smoothing * Time.deltaTime);
            currentElevation = Mathf.Lerp(currentElevation, targetElevation, smoothing * Time.deltaTime);

            // 应用旋转
            azimuthGimbal.localRotation = Quaternion.Euler(azimuthGimbalOffset + new Vector3(currentAzimuth, 0, 0));
            elevationGimbal.localRotation = Quaternion.Euler(elevationGimbalOffset + new Vector3(0, 0, currentElevation));

            //podCamera.LookAt(transform.position + podCameraGimbal.forward, Vector3.up);
        }
    }
}
