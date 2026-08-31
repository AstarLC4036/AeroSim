using AeroSim.AeroPhysics;
using AeroSim.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class DopplerPitch : MonoBehaviour
    {
        public static float soundSpeed = 343.2f;
        public Transform listener;
        private AudioSource audioSource;
        [HideInInspector]
        public float defaultPitch;

        private Vector3 lastPos;
        private Vector3 targetLastPos;
        private Vector3 velocity;
        private Vector3 velocityTarget;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            defaultPitch = audioSource.pitch;

            AudioListener audioListener = FindFirstObjectByType<AudioListener>();
            if (audioListener == null)
            {
                enabled = false;
                return;
            }

            listener = audioListener.transform;

            lastPos = transform.position;
            targetLastPos = listener.transform.position;

            OriginKeeper.onOriginChange += OnOriginChange;
        }

        private void FixedUpdate()
        {
            if (listener == null) return;

            Vector3 deltaPos = transform.position - lastPos;
            Vector3 targetDeltaPos = listener.transform.position - targetLastPos;
            velocity = deltaPos.normalized * (deltaPos.magnitude / Time.fixedDeltaTime);
            velocityTarget = targetDeltaPos.normalized * (targetDeltaPos.magnitude / Time.fixedDeltaTime);
            lastPos = transform.position;
            targetLastPos = listener.transform.position;
        }

        void OnOriginChange(Vector3 delta)
        {
            lastPos += delta;
            targetLastPos += delta;
        }

        void Update()
        {
            if (listener == null) return;

            // 计算相对速度向量
            Vector3 relativeVelocity = velocity - velocityTarget;

            // 方向：从声源指向监听器
            Vector3 directionToListener = (listener.position - transform.position).normalized;

            // 径向速度：相对速度在方向上的投影（正值表示远离）
            float radialVelocity = Vector3.Dot(relativeVelocity, directionToListener);

            // 多普勒频移计算
            float pitchFactor = soundSpeed / (soundSpeed + radialVelocity);
            audioSource.pitch = Mathf.Clamp(defaultPitch * pitchFactor, 0.5f, 2.0f);
        }
    }
}
