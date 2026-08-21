using AeroSim.InputSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Utils
{
    public class OriginKeeper : MonoBehaviour
    {
        public float limit;
        public Transform centerObject;
        public Transform[] externalObjects;

        public static Vector3 origin;

        public static Action<Vector3> onOriginChange = (delta) => { };

        public List<ParticleSystem> worldParticleSystems = new List<ParticleSystem>();
        public List<TrailRenderer> worldTrailRenderers = new List<TrailRenderer>();
        //public List<VisualEffect> worldVisualEffects = new List<VisualEffect>();
        
        public readonly int worldOffsetProp = Shader.PropertyToID("_WorldOriginOffset");

        public Material skyboxMaterial;


        // 在场景加载时注册粒子系统
        void Start()
        {
            // 查找场景中所有模拟空间为 World 的粒子系统
            ParticleSystem[] allPS = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in allPS)
            {
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
                {
                    worldParticleSystems.Add(ps);
                }
            }

            // 查找场景中所有拖尾渲染器
            TrailRenderer[] allTR = FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None);
            worldTrailRenderers.AddRange(allTR);

            //// 查找场景中所有视觉效果
            //VisualEffect[] allVE = FindObjectsByType<VisualEffect>(FindObjectsSortMode.None);
            //worldVisualEffects.AddRange(allVE);

            onOriginChange += ShiftOrigin;

            //Set atmosphere height offset for skybox shader
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetFloat("_DeltaHeight", -origin.y / 1000);
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            if(centerObject.position.x > limit)
            {
                int multiply = (int)(centerObject.position.x / limit);
                onOriginChange(new Vector3(-limit * multiply, 0, 0));
            }
            else if(centerObject.position.x < -limit)
            {
                int multiply = (int)(centerObject.position.x / -limit);
                onOriginChange(new Vector3(multiply * limit, 0, 0));
            }
            if(centerObject.position.y > limit)
            {
                int multiply = (int)(centerObject.position.y / limit);
                onOriginChange(new Vector3(0, multiply * -limit, 0));
            }
            else if(centerObject.position.y < -limit)
            {
                int multiply = (int)(centerObject.position.y / -limit);
                onOriginChange(new Vector3(0, multiply * limit, 0));
            }
            if(centerObject.position.z > limit)
            {
                int multiply = (int)(centerObject.position.z / limit);
                onOriginChange(new Vector3(0, 0, multiply * -limit));
            }
            else if(centerObject.position.z < -limit)
            {
                int multiply = (int)(centerObject.position.z / -limit);
                onOriginChange(new Vector3(0, 0, multiply * limit));
            }
        }

        void ShiftOrigin(Vector3 delta)
        {
            origin += delta;

            centerObject.position += delta;
            foreach (Transform ext in externalObjects)
                ext.position += delta;

            // 手动偏移所有世界空间粒子
            foreach (var ps in worldParticleSystems)
            {
                if (ps == null) continue;
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[ps.particleCount];
                int count = ps.GetParticles(particles);
                for (int i = 0; i < count; i++)
                {
                    particles[i].position += delta;
                }
                ps.SetParticles(particles, count);
            }

            foreach (TrailRenderer trail in worldTrailRenderers)
            {
                Vector3[] points = new Vector3[trail.positionCount];

                trail.GetPositions(points);
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] += delta;
                }
                trail.SetPositions(points);
            }

            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetFloat("_DeltaHeight", -origin.y / 1000);
            }
        }
    }
}