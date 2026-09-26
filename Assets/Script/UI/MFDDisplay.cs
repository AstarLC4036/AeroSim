using AeroSim.AircraftModules;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AeroSim.UI
{
    /// <summary>
    /// MFD Display content types
    /// </summary>
    public enum MFDType
    {
        None,
        RadarPPI,
        RadarBScope,
        Gimbal,
        RWR,
        Weapon,
        TargetingPod,
    }

    /// <summary>
    /// Component version of <see cref="MFDDrawer"/>, for UI on the screen.
    /// </summary>
    public class MFDDisplay : MonoBehaviour
    {
        public List<RawImage> drawTargets = new List<RawImage>();
        public MFDGraphicHelper drawer;
        public RenderTexture canvasTexture;
        public Vector2Int size = new Vector2Int(256, 256);
        public Color32 bgColor = new Color32(25, 25, 25, 255);

        public void InitCanvas()
        {
            Dispose();
            if (canvasTexture != null) // 重复初始化 = 旧 RT 永久泄漏（Persistent allocation）
            {
                drawer?.Dispose();     // 旧的 helper 也要释放，否则它的 ComputeBuffer 一样会漏
                drawer = null;
                canvasTexture.Release();
                GameObject.Destroy(canvasTexture);
                canvasTexture = null;
            }

            canvasTexture = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            canvasTexture.Create();

            drawer = new MFDGraphicHelper(canvasTexture, size.x, size.y);

            foreach (RawImage rawImage in drawTargets)
            {
                rawImage.texture = canvasTexture;
            }
        }

        protected virtual void Update()
        {
            UpdateCanvas();
        }

        /// <summary>
        /// Draw canvas.
        /// </summary>
        public virtual void ProcessCanvas()
        {
            // Draw here
        }

        public virtual void UpdateCanvas()
        {
            drawer.DrawRectFill(0, 0, size.x, size.y, bgColor);
            ProcessCanvas();
            ApplyTexture();
        }

        public void ApplyTexture()
        {
            drawer.Submit();
        }

        public void Dispose()
        {
            if (drawer != null) { drawer.Dispose(); drawer = null; }
            if (canvasTexture != null)
            {
                canvasTexture.Release();                     // 释放 native/GPU 内存（Leak Detected 的元凶）
                if (Application.isPlaying) Destroy(canvasTexture);
                else DestroyImmediate(canvasTexture);
                canvasTexture = null;
            }
        }

        private void OnApplicationQuit()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Graphic drawer for cockpit MFD displays.
    /// </summary>
    [Serializable]
    public class MFDDrawer
    {
        public MFDGraphicHelper drawer;
        public RenderTexture canvasTexture;
        public Vector2Int size = new Vector2Int(256, 256);
        public Color32 bgColor = new Color32(25, 25, 25, 255);

        public MFDDrawer(Vector2Int size, Color32 bgColor)
        {
            this.size = size;
            this.bgColor = bgColor;
        }

        public void InitCanvas()
        {
            Dispose();
            if (canvasTexture != null)
            {
                drawer?.Dispose();     // 旧的 helper 也要释放，否则它的 ComputeBuffer 一样会漏
                drawer = null;
                canvasTexture.Release();
                GameObject.Destroy(canvasTexture);
                canvasTexture = null;
            }

            canvasTexture = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            drawer = new MFDGraphicHelper(canvasTexture, size.x, size.y);
        }

        /// <summary>
        /// Draw canvas.
        /// </summary>
        public virtual void ProcessCanvas()
        {
            // Draw here
        }

        public virtual void UpdateCanvas()
        {
            drawer.DrawRectFill(0, 0, size.x, size.y, bgColor);
            ProcessCanvas();
            ApplyTexture();
        }

        public void ApplyTexture()
        {
            drawer.Submit();
        }

        /// <summary>
        /// Dispose drawer and texture memory
        /// </summary>
        public void Dispose()
        {
            if (drawer != null) { drawer.Dispose(); drawer = null; }
            if (canvasTexture != null)
            {
                canvasTexture.Release(); // Release native/GPU Memory (to avoid leak)
                if (Application.isPlaying) UnityEngine.Object.Destroy(canvasTexture);
                else UnityEngine.Object.DestroyImmediate(canvasTexture);
                canvasTexture = null;
            }
        }
    }
}