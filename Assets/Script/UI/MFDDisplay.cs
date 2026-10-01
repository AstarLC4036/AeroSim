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
            if (canvasTexture != null) // Re-initialising = the old RT leaks permanently (Persistent allocation)
            {
                drawer?.Dispose();     // The old helper has to be released too, or its ComputeBuffer leaks just the same
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
                canvasTexture.Release();                     // Release native/GPU memory (the culprit behind "Leak Detected")
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
                drawer?.Dispose();     // The old helper has to be released too, or its ComputeBuffer leaks just the same
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