using AeroSim.AircraftModules;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AeroSim.UI
{
    public enum MFDType
    {
        None,
        Radar,
        RWR,
        Weapon,
        TargetingPod,
    }

    //no what the fuck!?
    /// <summary>
    /// Component version of <see cref="MFDDrawer"/>, for UI on the screen.
    /// </summary>
    public class MFDDisplay : MonoBehaviour
    {
        public List<RawImage> drawTargets = new List<RawImage>();
        public int resolution = 260;
        protected Texture2D canvasTex;
        protected Color32[] pixels;
        protected MFDGraphicHelper32 drawer;

        public Color32 bgColor = new Color32(0, 185, 0, 128);

        public void InitCanavs()
        {
            canvasTex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
            foreach (RawImage rawImage in drawTargets)
            {
                rawImage.texture = canvasTex;
            }
            pixels = new Color32[resolution * resolution];

            Array.Fill(pixels, bgColor);
            canvasTex.SetPixels32(pixels);

            drawer = new MFDGraphicHelper32(pixels, resolution, resolution);

            canvasTex.wrapMode = TextureWrapMode.Clamp;
            canvasTex.filterMode = FilterMode.Point;
        }

        protected virtual void Update()
        {
            UpdateCanvas();
        }

        public virtual void ProcessCanvas()
        {
            // Draw here
        }

        public virtual void UpdateCanvas()
        {
            Array.Fill(pixels, bgColor);
            ProcessCanvas();
            ApplyTexture();
        }

        public void ApplyTexture()
        {
            canvasTex.SetPixels32(pixels);
            canvasTex.Apply();
        }
    }

    /// <summary>
    /// Graphic drawer for cockpit MFD displays.
    /// </summary>
    [Serializable]
    public class MFDDrawer
    {
        public Vector2Int size = new Vector2Int(256, 256);
        protected Texture2D canvasTex;
        protected Color32[] pixels;
        protected MFDGraphicHelper32 drawer;

        public Texture2D CanvasTexture => canvasTex;

        [SerializeField]
        public Color32 bgColor = new Color32(25, 25, 25, 255);

        public void InitCanavs()
        {
            canvasTex = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false);
            pixels = new Color32[size.x * size.y];

            Array.Fill(pixels, bgColor);
            canvasTex.SetPixels32(pixels);

            drawer = new MFDGraphicHelper32(pixels, size.x, size.y);

            canvasTex.wrapMode = TextureWrapMode.Clamp;
            canvasTex.filterMode = FilterMode.Point;
        }

        public virtual void ProcessCanvas()
        {
            // Draw here
        }

        public virtual void UpdateCanvas()
        {
            Array.Fill(pixels, bgColor);
            ProcessCanvas();
            ApplyTexture();
        }

        public void ApplyTexture()
        {
            canvasTex.SetPixels32(pixels);
            canvasTex.Apply();
        }
    }
}