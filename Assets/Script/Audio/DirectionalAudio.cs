using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AeroSim.Audio
{
    //Based on Deepseek's code
    [RequireComponent(typeof(AudioSource))]
    public class DirectionalAudio : MonoBehaviour
    {
        [Header("Directional Settings")]
        [Tooltip("正后方最大音量的角度范围（0°=正后方）")]
        public float maxAngle = 30f;
        [Tooltip("90°侧面时的最小音量")]
        public float minVolume = 0.2f;

        public float baseVolume = 1.0f;

        [Header("Distance Settings")]
        public float minDistance = 10f;   // 最大音量保持距离
        public float maxDistance = 300f;  // 声音消失距离

        private AudioSource audioSource;
        private Transform listener;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            listener = FindFirstObjectByType<AudioListener>().transform;
        }

        void Update()
        {
            if (listener == null) return;

            // 1. 距离衰减
            float dist = Vector3.Distance(transform.position, listener.position);
            float distanceVolume = 1f - Mathf.Clamp01((dist - minDistance) / (maxDistance - minDistance));

            // 2. 方向衰减 (后方最大)
            Vector3 dirToListener = (listener.position - transform.position).normalized;
            Vector3 backDirection = -transform.forward;      // 喷气方向的反方向
            float angle = Vector3.Angle(dirToListener, backDirection);
            float angleVolume = Mathf.Lerp(1f, minVolume, Mathf.Clamp01(angle / maxAngle));

            // 3. 最终音量
            audioSource.volume = distanceVolume * angleVolume * baseVolume;
        }

        public void OnDrawGizmosSelected()
        {
            Handles.DrawWireDisc(transform.position, transform.forward, 0);
            Handles.DrawWireDisc(transform.position - transform.forward * 10, transform.forward, 10 * Mathf.Tan(maxAngle * Mathf.Deg2Rad));
            Gizmos.DrawRay(transform.position, (-transform.forward + transform.up * Mathf.Tan(maxAngle * Mathf.Deg2Rad)) * 100);
            Gizmos.DrawRay(transform.position, (-transform.forward - transform.up * Mathf.Tan(maxAngle * Mathf.Deg2Rad)) * 100);
            Gizmos.DrawRay(transform.position, (-transform.forward + transform.right * Mathf.Tan(maxAngle * Mathf.Deg2Rad)) * 100);
            Gizmos.DrawRay(transform.position, (-transform.forward - transform.right * Mathf.Tan(maxAngle * Mathf.Deg2Rad)) * 100);
        }
    }
}
