using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore;

namespace AeroSim.UI
{
    /// <summary>
    /// Blend mode. Written into DrawCommand.layer (C# used to assign this field but the shader never read it, so it is reused here and the struct layout stays unchanged).
    /// </summary>
    public enum MFDBlend
    {
        /// <summary>Normal alpha blending (the default; behaviour unchanged).</summary>
        Over = 0,
        /// <summary>destination-out: only reduces the alpha of what is already drawn and writes no colour — used to "cut out" a region.</summary>
        Erase = 1,
        /// <summary>Straight overwrite, no blending (with alpha=1 it replaces the existing pixel with this colour).</summary>
        Replace = 2,
        /// <summary>
        /// Erase that applies "outside the shape": the same maths as Erase, but with the coverage inverted.
        /// Its purpose is to **clip what has already been drawn into a shape** (draw a wide area first, then use this to erase the part outside the shape).
        /// Note: erasing outside the shape leaves it **transparent** (revealing the black background behind) — use <see cref="FillOutsideDisc"/> or OverOutside to keep a background colour.
        /// Only the three values 3/4/5 carry the "outside the shape" semantics; any other layer value (including a mistyped int) is treated as 0 and will not suddenly take effect.
        /// </summary>
        EraseOutside = 3,
        /// <summary>
        /// Normal alpha coverage **outside** the shape (the same maths as Over, with the coverage inverted).
        /// Typical use: after the attitude ball has drawn sky and ground, paint the area outside the circle back to the MFD grey background ✓ works for any primitive.
        /// </summary>
        OverOutside = 4,
        /// <summary>Straight overwrite **outside** the shape, no blending (the inverted version of Replace).</summary>
        ReplaceOutside = 5,
    }

    /// <summary>Horizontal anchor for text: which edge of the text x refers to. Default = use MFDGraphicHelper.DefaultAnchor.</summary>
    public enum MFDTextAnchor
    {
        Default = 0,
        /// <summary>x is the left edge of the text (default).</summary>
        Left = 1,
        /// <summary>x is the horizontal centre of the text (when right-aligning, pass the same x and two runs of text line up on the same right edge).</summary>
        Center = 2,
        /// <summary>x is the right edge of the text.</summary>
        Right = 3,
    }

    public class MFDGraphicHelper
    {
        private static MFDGraphicHelper instance;
        public static MFDGraphicHelper Instance
        {
            get
            {
                if (instance == null)
                    instance = new MFDGraphicHelper();
                return instance;
            }
        }

        public enum DrawCommandType
        {
            Line = 0,
            DashedLine = 1,
            Circle = 2,
            RectOutline = 3,
            RectFill = 4,
            Texture = 5,
            Text = 6,
            Disc = 7,
            RotatedRectFill = 8,
            SetClipRect = 9,
            ClearClip = 10,
            Arc = 11,
            Triangle = 12
        }

        public struct DrawCommand
        {
            public int type;
            public Vector4 param1;
            public Vector4 param2;
            public Vector4 color;
            public int dashLength;
            public int gapLength;
            public int layer;       // Blend mode, see MFDBlend (0=Over 1=Erase 2=Replace). Draw order = command order
        }

        public ComputeShader mfdCompute;
        private RenderTexture outputRT;
        private ComputeBuffer commandBuffer;
        private List<DrawCommand> commands = new List<DrawCommand>();
        private Texture2DArray mfdArray;
        private RenderTexture radarTexture;
        private int kernelIndex;
        private int width;
        private int height;

        private bool registered;
        private static readonly List<MFDGraphicHelper> liveHelpers = new List<MFDGraphicHelper>();

        // ================= Design resolution scaling =================
        // Pages write coordinates/sizes/font sizes against designSize; the real canvas (the width/height given at construction) scales automatically.
        // Default (0,0) = disabled → every parameter is ×1 and the output is pixel-identical to before this feature existed.
        private Vector2Int designSize = Vector2Int.zero;

        /// <summary>Current design resolution; (0,0) means scaling is not enabled.</summary>
        public Vector2Int DesignSize => designSize;

        /// <summary>Device pixels per design unit. Always 1 while scaling is not enabled.</summary>
        public float DesignScale
        {
            get
            {
                if (designSize.x <= 0 || designSize.y <= 0 || width <= 0 || height <= 0) return 1f;
                // Take the smaller of the two axis ratios: should the aspect ratios ever disagree, leaving blank space beats cropping content
                return Mathf.Min((float)width / designSize.x, (float)height / designSize.y);
            }
        }

        /// <summary>
        /// Set the design resolution: pages write coordinates and font sizes against this size while the canvas resolution can change at any time (keep the aspect ratio matching the canvas).
        /// Pass (0,0) to disable scaling. The returned horizontal advance is still measured in design units.
        /// </summary>
        public void SetDesignSize(int w, int h) { designSize = new Vector2Int(w, h); }
        public void SetDesignSize(Vector2Int size) { designSize = size; }

        // Design units → device pixels (×1 while scaling is off)
        private float Scale(float v) => v * DesignScale;
        private Vector2 Scale(Vector2 v) => v * DesignScale;

        public int Width => width;
        public int Height => height;

        public MFDGraphicHelper(ComputeShader mfdCompute, RenderTexture texture, int width, int height)
        {
            this.mfdCompute = mfdCompute;
            this.width = width;
            this.height = height;

            kernelIndex = mfdCompute.FindKernel("DrawMFD");
            outputRT = texture;

            mfdCompute.SetTexture(kernelIndex, "_Result", outputRT);
            mfdCompute.SetInt("_Width", width);
            mfdCompute.SetInt("_Height", height);
            mfdCompute.SetTexture(kernelIndex, "_SourceTex", outputRT);
        }

        public MFDGraphicHelper(RenderTexture texture, int width, int height)
        {
            mfdCompute = MFDGraphicHelperSettings.MfdShader;
            this.width = width;
            this.height = height;

            kernelIndex = mfdCompute.FindKernel("DrawMFD");
            outputRT = texture;

            mfdCompute.SetTexture(kernelIndex, "_Result", outputRT);
            mfdCompute.SetInt("_Width", width);
            mfdCompute.SetInt("_Height", height);
            mfdCompute.SetTexture(kernelIndex, "_SourceTex", outputRT);
        }

        public MFDGraphicHelper()
        {

        }

        public void InitMFDDrawers(int width, int height, int layers)
        {
            mfdArray = new Texture2DArray(width, height, layers, TextureFormat.ARGB32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
        }

        public void RegistTexture(RenderTexture tex, int layer)
        {
            Graphics.CopyTexture(tex, 0, mfdArray, layer);
        }

        public void SetRadarTexture(RenderTexture tex)
        {
            radarTexture = tex;
        }

        public void UpdateShaderParam()
        {
            kernelIndex = mfdCompute.FindKernel("DrawMFD");
            mfdCompute.SetTexture(kernelIndex, "_Result", outputRT);
            mfdCompute.SetInt("_Width", width);
            mfdCompute.SetInt("_Height", height);
            mfdCompute.SetTexture(kernelIndex, "_SourceTex", outputRT);
        }

        public void DrawLine(Vector2 a, Vector2 b, Color color, int layer = 0)
        {
            DrawLine(a.x, a.y, b.x, b.y, color, layer);
        }

        public void DrawLine(float x0, float y0, float x1, float y1, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 0,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(x1), Scale(y1)),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>
        /// Overload with a line width; width is the **diameter in pixels** (1 = the original thin line, and <=1 looks exactly as it did before).
        /// It is a capsule SDF underneath, so both endpoints are round caps automatically; at corners, draw the ends of the wide lines overlapping and they join up naturally.
        /// </summary>
        public void DrawLine(Vector2 a, Vector2 b, Color color, float width, int layer = 0)
        {
            DrawLine(a.x, a.y, b.x, b.y, color, width, layer);
        }

        /// <summary>Overload with a line width (see above). Passing width as a float avoids ambiguity with the <c>int layer</c> overload.</summary>
        public void DrawLine(float x0, float y0, float x1, float y1, Color color, float width, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 0,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(x1), Scale(y1)),
                param2 = new Vector4(Scale(width), 0, 0, 0),   // param2 used to be unused for Line, so it carries the line width
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawDashedLine(Vector2 a, Vector2 b, Color color, int dashedlength = 4, int gapLength = 6, int layer = 0)
        {
            DrawDashedLine(a.x, a.y, b.x, b.y, color, dashedlength, gapLength, layer);
        }

        public void DrawDashedLine(float x0, float y0, float x1, float y1, Color color, int dashLength = 4, int gapLength = 6, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 1,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(x1), Scale(y1)),
                color = new Vector4(color.r, color.g, color.b, color.a),
                dashLength = Mathf.Max(Mathf.RoundToInt(Scale(dashLength)), 1),
                gapLength = Mathf.Max(Mathf.RoundToInt(Scale(gapLength)), 0),
                layer = layer
            });
        }

        public void DrawCircle(Vector2 center, float radius, float thickness, Color color, int layer = 0)
        {
            DrawCircle(center.x, center.y, radius, thickness, color, layer);
        }

        public void DrawCircle(float x0, float y0, float radius, float thickness, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 2,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(radius), Scale(thickness)),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>
        /// Filled disc. param1 = centre + radius; the antialiasing is the same as for the ring (1px).
        /// Combined with <see cref="MFDBlend.Erase"/> it becomes a "circular hole punch" — useful for the attitude ball, for clipping a rectangle into a circle, and for a reticle mask.
        /// </summary>
        public void DrawDisc(Vector2 center, float radius, Color color, int layer = 0)
        {
            DrawDisc(center.x, center.y, radius, color, layer);
        }

        public void DrawDisc(float x0, float y0, float radius, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 7,
                param1 = new Vector4(Scale(x0), Scale(y0), Mathf.Max(Scale(radius), 0f), 0f),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>Filled-disc erase (destination-out, no blending): alpha=1 punches straight through, 0.5 removes only half.</summary>
        public void DrawDiscErase(float x0, float y0, float radius, float alpha = 1f)
        {
            DrawDisc(x0, y0, radius, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        /// <summary>
        /// Keep only what is inside the circle: erase everything **outside** it. Equivalent to "clipping to a circle" (stencil).
        /// Usage: draw over a wide area first, then call this once and the excess is gone.
        /// </summary>
        public void DrawDiscStencil(float x0, float y0, float radius, float alpha = 1f)
        {
            DrawDisc(x0, y0, radius, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.EraseOutside);
        }

        /// <summary>
        /// Paint everything **outside** the circle in the given colour (normal alpha coverage) — the "no black background showing" version of <see cref="DrawDiscStencil"/>.
        /// The standard attitude-ball usage: after drawing sky/ground (which overflows), call this once and the area outside the circle returns to the MFD background colour ✓
        /// Other shapes work the same way: <c>DrawRectFill(..., (int)MFDBlend.OverOutside)</c> paints away the area outside a rectangle.
        /// </summary>
        public void FillOutsideDisc(Vector2 center, float radius, Color color)
        {
            DrawDisc(center, radius, color, (int)MFDBlend.OverOutside);
        }

        public void FillOutsideDisc(float x0, float y0, float radius, Color color)
        {
            DrawDisc(x0, y0, radius, color, (int)MFDBlend.OverOutside);
        }

        /// <summary>
        /// Arc: an **annulus** filled only within an angular range (the green arc on a tachometer).
        /// Angles are in degrees, 0° = +X direction, counter-clockwise positive (matching Mathf.Cos/Sin) ✓
        /// endDeg &lt; startDeg sweeps clockwise ✓ the angular edges are hard (the radial edges still get 1px antialiasing ✓)
        /// thickness &lt;= 0 turns it into a **sector** (filled from the centre all the way out to the radius) ✓
        /// </summary>
        public void DrawArc(Vector2 center, float radius, float thickness,
                            float startDeg, float endDeg, Color color, int layer = 0)
        {
            DrawArc(center.x, center.y, radius, thickness, startDeg, endDeg, color, layer);
        }

        public void DrawArc(float cx, float cy, float radius, float thickness,
                            float startDeg, float endDeg, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 11,
                param1 = new Vector4(Scale(cx), Scale(cy), Mathf.Max(Scale(radius), 0f), Scale(Mathf.Max(thickness, 0f))),
                param2 = new Vector4(startDeg, endDeg, 0f, 0f),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>Fill an arc by a 0~1 value (the most common gauge case): value=0 draws nothing, 1 fills the whole span ✓</summary>
        public void DrawArcValue(Vector2 center, float radius, float thickness,
                                 float startDeg, float endDeg, float value01,
                                 Color color, int layer = 0)
        {
            DrawArcValue(center.x, center.y, radius, thickness, startDeg, endDeg, value01, color, layer);
        }

        public void DrawArcValue(float cx, float cy, float radius, float thickness,
                                 float startDeg, float endDeg, float value01,
                                 Color color, int layer = 0)
        {
            float end = Mathf.Lerp(startDeg, endDeg, Mathf.Clamp01(value01));
            DrawArc(cx, cy, radius, thickness, startDeg, end, color, layer);
        }

        /// <summary>Sector (filled from the centre out to the radius; a wrapper that passes thickness = 0 ✓).</summary>
        public void DrawSector(Vector2 center, float radius, float startDeg, float endDeg,
                               Color color, int layer = 0)
        {
            DrawArc(center.x, center.y, radius, 0f, startDeg, endDeg, color, layer);
        }

        // ================= Triangles / arbitrary polygons (path filling) =================

        /// <summary>
        /// Filled triangle (1px antialiasing). The winding order does not matter; it is normalised to counter-clockwise internally.
        /// This is the base primitive of <see cref="FillPolygon"/> and can also be used directly to draw triangular markers.
        /// </summary>
        public void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color, int layer = 0)
        {
            // Normalise to counter-clockwise (the half-plane SDF in the shader depends on the winding)
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (cross < 0f) { Vector2 tmp = b; b = c; c = tmp; }

            commands.Add(new DrawCommand
            {
                type = 12,
                param1 = new Vector4(Scale(a.x), Scale(a.y), Scale(b.x), Scale(b.y)),
                param2 = new Vector4(Scale(c.x), Scale(c.y), 0f, 0f),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>
        /// Join a list of points into a closed path and **fill the interior** (convex and concave both work: ear-clipping triangulation internally).
        /// Returns how many triangle commands were produced (= point count - 2; self-intersecting or degenerate polygons bail out early).
        /// Note: every call allocates a few temporary Lists; if you draw many large polygons per frame, cache the vertex array instead.
        /// Tip: after filling, add a <see cref="DrawPolygonOutline"/> pass to cover the AA seams between adjacent triangles.
        /// </summary>
        public int FillPolygon(IList<Vector2> points, Color color, int layer = 0)
        {
            if (points == null || points.Count < 3) return 0;

            // Signed area → decides the index order (normalised to counter-clockwise)
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i], q = points[(i + 1) % points.Count];
                area += p.x * q.y - q.x * p.y;
            }

            List<int> idx = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++)
                idx.Add(area >= 0f ? i : points.Count - 1 - i);

            int made = 0;
            int guard = idx.Count * idx.Count + 8;      // Guards against an infinite loop on degenerate input

            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = points[i0], b = points[i1], c = points[i2];

                    // Under counter-clockwise winding only cross > 0 is a convex ear
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f) continue;

                    bool isEar = true;
                    for (int j = 0; j < idx.Count && isEar; j++)
                    {
                        int ij = idx[j];
                        if (ij == i0 || ij == i1 || ij == i2) continue;
                        if (PointInTriangle(points[ij], a, b, c)) isEar = false;
                    }
                    if (!isEar) continue;

                    DrawTriangle(a, b, c, color, layer);
                    made++;
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;    // Self-intersecting or degenerate: drop the rest to avoid an infinite loop
            }

            if (idx.Count == 3)
            {
                DrawTriangle(points[idx[0]], points[idx[1]], points[idx[2]], color, layer);
                made++;
            }
            return made;
        }

        /// <summary>Join a list of points into a line (closed by default) to use as an outline. width &lt;= 1 gives the original 1px hairline.</summary>
        public void DrawPolygonOutline(IList<Vector2> points, float width, Color color,
                                       int layer = 0, bool closed = true)
        {
            if (points == null || points.Count < 2) return;
            int last = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < last; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                DrawLine(a.x, a.y, b.x, b.y, color, width, layer);
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        /// <summary>
        /// Set the clip rectangle: drawing afterwards **only takes effect inside the rectangle** (the equivalent of scissor).
        /// The parameters match <see cref="DrawRectFill(float,float,float,float,Color,int)"/>: (x0,y0) is a corner and w/h are the full width and height.
        /// This is how you build several individually clipped regions on one page; remember to <see cref="ClearClip"/> afterwards (it is also restored automatically when the per-frame command stream is cleared).
        /// </summary>
        public void SetClipRect(float x0, float y0, float w0, float h0)
        {
            commands.Add(new DrawCommand
            {
                type = 9,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(x0 + Mathf.Max(w0, 0f)), Scale(y0 + Mathf.Max(h0, 0f))),
                layer = 0
            });
        }

        public void SetClipRect(Rect rect)
        {
            SetClipRect(rect.xMin, rect.yMin, rect.width, rect.height);
        }

        /// <summary>Clear the clip and restore the full screen.</summary>
        public void ClearClip()
        {
            commands.Add(new DrawCommand { type = 10, layer = 0 });
        }

        /// <summary>
        /// Rotated filled rectangle: centre + half size + rotation angle (degrees). The rotation is passed into param2 as (cos, sin), so the shader needs no trigonometry per pixel.
        /// With <see cref="MFDBlend.Erase"/> it erases a slanted rectangle; with EraseOutside it makes a slanted clipping frame.
        /// </summary>
        public void DrawRotatedRectFill(Vector2 center, Vector2 halfSize, float angleDeg, Color color, int layer = 0)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            commands.Add(new DrawCommand
            {
                type = 8,
                param1 = new Vector4(Scale(center.x), Scale(center.y), Mathf.Max(Scale(halfSize.x), 0f), Mathf.Max(Scale(halfSize.y), 0f)),
                param2 = new Vector4(Mathf.Cos(rad), Mathf.Sin(rad), 0f, 0f),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>Rotated filled-rectangle erase.</summary>
        public void DrawRotatedRectErase(Vector2 center, Vector2 halfSize, float angleDeg, float alpha = 1f)
        {
            DrawRotatedRectFill(center, halfSize, angleDeg, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        /// <summary>
        /// Half-plane fill: (x0, y0) is a point on the dividing line and angleDeg is that line's tilt;
        /// it fills the **negative normal side** (the local -Y side, i.e. "below the line"). The attitude ball uses this for its sky/ground split:
        /// the dividing point moves up and down with pitch, the tilt rotates inversely with roll, and the lower half follows along.
        /// </summary>
        public void DrawHalfPlane(float x0, float y0, float angleDeg, float size, Color color, int layer = 0)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            // Local -Y direction = (s, -c); push the rectangle centre to one side of the line so that its top edge rests exactly on the dividing line
            Vector2 below = new Vector2(s, -c);
            Vector2 center = new Vector2(x0, y0) + below * size;
            DrawRotatedRectFill(center, new Vector2(size, size), angleDeg, color, layer);
        }

        public void DrawRectOutlineCenter(Vector2 center, Vector2 size, float thickness, Color color, int layer = 0)
        {
            DrawRectOutlineCenter(center.x, center.y, size.x, size.y, thickness, color, layer);
        }

        public void DrawRectOutlineCenter(float x0, float y0, float w0, float h0, float thickness, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 3,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(w0), Scale(h0)),
                param2 = new Vector4(Scale(thickness), 0, 0, 0),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectOutline(Vector2 center, Vector2 size, float thickness, Color color, int layer = 0)
        {
            DrawRectOutline(center.x, center.y, size.x, size.y, thickness, color, layer);
        }

        public void DrawRectOutline(float x0, float y0, float w0, float h0, float thickness, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 3,
                param1 = new Vector4(Scale(x0 + w0 / 2), Scale(y0 + h0 / 2), Scale(w0 / 2), Scale(h0 / 2)),
                param2 = new Vector4(Scale(thickness), 0, 0, 0),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectFillCenter(Vector2 center, Vector2 size, Color color, int layer = 0)
        {
            DrawRectFillCenter(center.x, center.y, size.x, size.y, color, layer);
        }

        public void DrawRectFillCenter(float x0, float y0, float w0, float h0, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 4,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(w0), Scale(h0)),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectFill(Vector2 center, Vector2 size, Color color, int layer = 0)
        {
            DrawRectFill(center.x, center.y, size.x, size.y, color, layer);
        }

        public void DrawRectFill(float x0, float y0, float w0, float h0, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 4,
                param1 = new Vector4(Scale(x0 + w0 / 2), Scale(y0 + h0 / 2), Scale(w0 / 2), Scale(h0 / 2)),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        /// <summary>
        /// "Cut" a rectangular region out of what has already been drawn (destination-out, no alpha blending).
        /// The parameters match <see cref="DrawRectFill(float,float,float,float,Color,int)"/>: (x0,y0) is a corner and w/h are the full width and height.
        /// Order matters: draw the content first, erase afterwards (each frame the canvas accumulates in command order).
        /// alpha=1 cuts straight through, 0.5 removes only half the alpha.
        /// </summary>
        public void DrawRectErase(float x0, float y0, float w0, float h0, float alpha = 1f)
        {
            DrawRectFill(x0, y0, w0, h0, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        /// <summary>Erase (centre + half-width/half-height version; the parameters match <see cref="DrawRectFillCenter(float,float,float,float,Color,int)"/>).</summary>
        public void DrawRectEraseCenter(float cx, float cy, float halfW, float halfH, float alpha = 1f)
        {
            DrawRectFillCenter(cx, cy, halfW, halfH, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        public void Submit()
        {
            if (!registered)
            {
                RegisterHelper();      // Safety net: release everything on quit (even if the owner forgets to Dispose)
            }

            mfdCompute.SetTexture(kernelIndex, "_Result", outputRT);
            mfdCompute.SetInt("_Width", width);
            mfdCompute.SetInt("_Height", height);
            mfdCompute.SetTexture(kernelIndex, "_SourceTex", outputRT);

            if (commands.Count == 0) return;

            if (commandBuffer == null || commandBuffer.count < commands.Count)
            {
                commandBuffer?.Release();
                // Grow the capacity in powers of two so the native buffer is not rebuilt over and over as commands are added one at a time
                int capacity = Mathf.Max(Mathf.NextPowerOfTwo(Mathf.Max(commands.Count, 1)), 64);
                commandBuffer = new ComputeBuffer(capacity,
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(DrawCommand)));
            }

            commandBuffer.SetData(commands);
            mfdCompute.SetBuffer(kernelIndex, "_Commands", commandBuffer);
            mfdCompute.SetInt("_CommandCount", commands.Count);

            // Text font atlas (only actually needed when a type==6 command is drawn)
            TMP_FontAsset font = TextFont;
            if (font != null && font.atlasTexture != null)
            {
                mfdCompute.SetTexture(kernelIndex, "_FontAtlas", font.atlasTexture);
                mfdCompute.SetInt("_FontAtlasChannel",
                    FontAtlasChannelOverride >= 0 ? FontAtlasChannelOverride : AtlasChannel(font));
            }
            else
            {
                // Safety net: keeps _FontAtlas from dangling (sampling an unbound SRV under D3D spams NULL SRV warnings)
                mfdCompute.SetTexture(kernelIndex, "_FontAtlas", Texture2D.whiteTexture);
                mfdCompute.SetInt("_FontAtlasChannel", 0);
            }

            mfdCompute.Dispatch(kernelIndex, width / 8, height / 8, 1);

            commands.Clear();
        }

        private void RegisterHelper()
        {
            if (registered) return;
            registered = true;
            liveHelpers.Add(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHelperRegistry()
        {
            liveHelpers.Clear();                 // With Domain Reload disabled, static fields survive across play sessions
            Application.quitting -= ReleaseAllHelpers;
            Application.quitting += ReleaseAllHelpers;
        }

        private static void ReleaseAllHelpers()
        {
            for (int i = liveHelpers.Count - 1; i >= 0; i--)
            {
                MFDGraphicHelper h = liveHelpers[i];
                if (h != null) h.Dispose();
            }
            liveHelpers.Clear();
        }

        public void Dispose()
        {
            liveHelpers.Remove(this);
            registered = false;
            commandBuffer?.Release();
            commandBuffer = null;
        }

        // ===================== Text drawing (TMP SDF atlas, single Dispatch) =====================
        public const float DefaultTextSharpness = 0.1f;

        /// <summary>
        /// Whether the atlas glyphRect.y is measured from the "top". TMP officially uses a bottom-left origin (see vertex_BL.uv = glyphRect.y/H in TMP_Text.cs),
        /// hence the default of false. If a particular font renders blank or misplaced, try flipping this switch first.
        /// </summary>
        public static bool FlipGlyphV = false;

        /// <summary>Whether the SDF atlas padding margin is drawn into the quad as well (what TMP does; softer, more complete edges).</summary>
        public static bool TextUseAtlasPadding = true;

        /// <summary>Debug only: when >=0, force the atlas channel. 0=alpha, 1=red, 2=solid block (to confirm quad placement).</summary>
        public static int FontAtlasChannelOverride = -1;

        private TMP_FontAsset textFont;
        private static bool warnedNoFont;

        private static void WarnNoFont()
        {
            if (warnedNoFont) return;
            warnedNoFont = true;
            Debug.LogWarning("[MFD] 文字没有绘制：没有可用字体。请在场景里给 MFDGraphicHelperSettings.defaultFont 指定一个 TMP Font Asset，或设置 MFDGraphicHelper.TextFont。");
        }

        private static TMP_FontAsset fallbackFont;
        private static bool fallbackSearched;

        /// <summary>Last-resort fallback: the MFD/HUD font in Resources, so text still draws with no configuration.</summary>
        private static TMP_FontAsset FallbackFont()
        {
            if (!fallbackSearched)
            {
                fallbackSearched = true;
                fallbackFont = Resources.Load<TMP_FontAsset>("Fonts/HUD-ShareTechMono-Regular SDF");
                if (fallbackFont == null) fallbackFont = Resources.Load<TMP_FontAsset>("Fonts/MSYH SDF");
            }
            return fallbackFont;
        }

        /// <summary>Font used by this drawer; when unset it falls back to MFDGraphicHelperSettings.defaultFont and then to the default Resources font.</summary>
        public TMP_FontAsset TextFont
        {
            get
            {
                if (textFont != null) return textFont;
                MFDGraphicHelperSettings s = MFDGraphicHelperSettings.Instance;
                if (s != null && s.defaultFont != null) return s.defaultFont;
                return FallbackFont();
            }
            set { textFont = value; }
        }

        /// <summary>
        /// Letter spacing (tracking): the extra spacing added between each character, measured as a **fraction of the font size** (em).
        /// 0.05 = 5% of the font size of extra space between characters; negative values tighten it. 0 is the font's own advance.
        /// It is only added between characters, so the widths and centring of MeasureText / DrawTextCentered stay correct.
        /// </summary>
        public float LetterSpacing = 0f;

        /// <summary>Default anchor used when DrawText is not given an explicit anchor (horizontal direction only).</summary>
        public MFDTextAnchor DefaultAnchor = MFDTextAnchor.Left;

        /// <summary>
        /// Draw one line of text at (x, y). The coordinate system matches the other Draw* calls: y points up and (x, y) is the start of the line's **baseline**.
        /// anchor decides which edge of the text x refers to: Left (default, x = left edge) / Center (x = horizontal centre) / Right (x = right edge).
        /// Returns the horizontal advance (in pixels) so the next run can be drawn straight after it.
        /// </summary>
        public float DrawText(string text, float x, float y, Color color,
                              float size = 16f, float sharpness = DefaultTextSharpness,
                              MFDTextAnchor anchor = MFDTextAnchor.Default,
                              float letterSpacing = float.NaN)
        {
            return LayoutText(text, Scale(x), Scale(y), color, Scale(size), sharpness,
                              ResolveAnchor(anchor), false, 0f, letterSpacing) / DesignScale;
        }

        /// <summary>Same as above (positional-parameter version; the 7th parameter is the letter spacing). The anchor comes from <see cref="DefaultAnchor"/>.</summary>
        public float DrawText(string text, float x, float y, Color color,
                              float size, float sharpness, float tracking)
        {
            return LayoutText(text, Scale(x), Scale(y), color, Scale(size), sharpness,
                              ResolveAnchor(MFDTextAnchor.Default), false, 0f, tracking) / DesignScale;
        }

        /// <summary>Draw one line of text centred on (cx, cy) (horizontally centred, vertically centred on the cap height).</summary>
        public float DrawTextCentered(string text, float cx, float cy, Color color,
                                      float size = 16f, float sharpness = DefaultTextSharpness,
                                      float letterSpacing = float.NaN)
        {
            return LayoutText(text, Scale(cx), Scale(cy), color, Scale(size), sharpness,
                              MFDTextAnchor.Center, true, Scale(cy), letterSpacing) / DesignScale;
        }

        private MFDTextAnchor ResolveAnchor(MFDTextAnchor anchor)
        {
            return anchor == MFDTextAnchor.Default ? DefaultAnchor : anchor;
        }

        /// <summary>Measure the width only (in pixels) without producing draw commands. Uses the current <see cref="LetterSpacing"/>.</summary>
        public float MeasureText(string text, float size)
        {
            return MeasureText(text, size, LetterSpacing);
        }

        /// <summary>Measure the width only (in design units). tracking means the same as DrawText's letterSpacing.</summary>
        public float MeasureText(string text, float size, float letterSpacing)
        {
            return MeasureTextDevice(text, Scale(size), letterSpacing) / DesignScale;
        }

        // Measurement in device-pixel space (internal: LayoutText already works in device space)
        private float MeasureTextDevice(string text, float size, float letterSpacing)
        {
            TMP_FontAsset font = TextFont;
            if (font == null || string.IsNullOrEmpty(text) || font.characterLookupTable == null) return 0f;
            if (float.IsNaN(letterSpacing)) letterSpacing = LetterSpacing;

            float px = size / font.faceInfo.pointSize * font.faceInfo.scale;
            float tracking = letterSpacing * size;
            float w = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                if (font.characterLookupTable.TryGetValue(text[i], out TMP_Character c) && c != null)
                    w += c.glyph.metrics.horizontalAdvance * px;
                if (i < text.Length - 1) w += tracking;      // Letter spacing is only counted between characters
            }
            return w;
        }

        private float LayoutText(string text, float x, float y, Color color, float size, float sharpness,
                                 MFDTextAnchor anchor, bool verticalMiddle, float vCenterY, float letterSpacing)
        {
            if (float.IsNaN(letterSpacing)) letterSpacing = LetterSpacing;

            TMP_FontAsset font = TextFont;
            if (font == null) { WarnNoFont(); return 0f; }
            if (string.IsNullOrEmpty(text)) return 0f;

            // Dynamic font: a glyph may not be in the atlas yet (not found in characterLookupTable → it silently fails to draw)
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                font.TryAddCharacters(text, out string _);

            if (font.characterLookupTable == null || font.atlasTexture == null) return 0f;

            Texture atlas = font.atlasTexture;
            float atlasW = atlas.width;
            float atlasH = atlas.height;
            float px = size / font.faceInfo.pointSize * font.faceInfo.scale;
            int sharp = Mathf.Clamp(Mathf.RoundToInt(sharpness * 1000f), 1, 8000);

            float lineWidth = MeasureTextDevice(text, size, letterSpacing);
            float tracking = letterSpacing * size;          // pixels

            // Horizontal anchor: x means left edge / centre / right edge
            float originX = anchor == MFDTextAnchor.Right ? x - lineWidth
                          : anchor == MFDTextAnchor.Center ? x - lineWidth * 0.5f
                          : x;
            float penX = Mathf.Round(originX);
            float baseline = Mathf.Round(verticalMiddle ? vCenterY - font.faceInfo.capLine * px * 0.5f : y);

            for (int i = 0; i < text.Length; i++)
            {
                bool hasGlyph = font.characterLookupTable.TryGetValue(text[i], out TMP_Character ch) && ch != null;

                if (hasGlyph)
                {
                    Glyph glyph = ch.glyph;
                    GlyphRect r = glyph.glyphRect;

                    if (r.width > 0 && r.height > 0)
                    {
                        // Target rectangle (pixel alignment is the key to crisp text)
                        float x0 = Mathf.Round(penX + glyph.metrics.horizontalBearingX * px);
                        float x1 = Mathf.Round(penX + (glyph.metrics.horizontalBearingX + r.width) * px);
                        float y1 = Mathf.Round(baseline + glyph.metrics.horizontalBearingY * px);   // top
                        float y0 = Mathf.Round(y1 - r.height * px);                                  // bottom

                        float u0 = r.x / atlasW;
                        float u1 = (r.x + r.width) / atlasW;
                        float vA = r.y / atlasH;                        // glyphRect bottom edge (TMP: bottom-left origin)
                        float vB = (r.y + r.height) / atlasH;           // glyphRect top edge
                        float v0 = FlipGlyphV ? 1f - vB : vA;
                        float v1 = FlipGlyphV ? 1f - vA : vB;

                        // SDF padding: sample and draw the distance-field margin around the glyph in the atlas as well, so the edge transition is complete
                        if (TextUseAtlasPadding && font.atlasPadding > 0)
                        {
                            float pad = font.atlasPadding;
                            float pu = pad / atlasW;
                            float pv = pad / atlasH;
                            u0 = Mathf.Max(0f, u0 - pu);
                            u1 = Mathf.Min(1f, u1 + pu);
                            v0 = Mathf.Max(0f, v0 - pv);
                            v1 = Mathf.Min(1f, v1 + pv);

                            float ppx = pad * px;
                            x0 -= ppx; x1 += ppx; y0 -= ppx; y1 += ppx;
                        }

                        if (x1 <= x0) x1 = x0 + 1f;
                        if (y1 <= y0) y1 = y0 + 1f;

                        commands.Add(new DrawCommand
                        {
                            type = 6,
                            param1 = new Vector4(x0, y0, x1, y1),
                            param2 = new Vector4(u0, v0, u1, v1),
                            color = new Vector4(color.r, color.g, color.b, color.a),
                            dashLength = sharp,     // Reused as sharpness*1000 (keeps the DrawCommand layout unchanged)
                            gapLength = 0,
                            layer = 0
                        });
                    }

                    penX += glyph.metrics.horizontalAdvance * px;
                }

                if (i < text.Length - 1) penX += tracking;      // Letter spacing is only added between characters (consistent with MeasureText)
            }
            return lineWidth;
        }

        /// <summary>Channel holding the SDF value in the TMP atlas: Alpha8 → alpha(0), R8/R16 → red(1).</summary>
        private static int AtlasChannel(TMP_FontAsset font)
        {
            Texture2D t = font != null ? font.atlasTexture : null;
            if (t == null) return 0;
            switch (t.format)
            {
                case TextureFormat.R8:
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RFloat:
                    return 1;
                default:
                    return 0;
            }
        }

        // old cpu version
        /*
        private Color32[] pixels;
        private int height;
        private int width;

        public MFDGraphicHelper32(Color32[] pixels, int width, int height)
        {
            this.pixels = pixels;
            this.width = width;
            this.height = height;
        }

        public int PixelIndex(int x, int y)
        {
            return PixelIndex(x, y, width, height);
        }

        public void DrawLine(int x0, int y0, int x1, int y1, Color32 color)
        {
            DrawLine(pixels, x0, y0, x1, y1, width, height, color);
        }

        public void DrawDashedLine(int x0, int y0, int x1, int y1, Color32 color, int dashLength = 6, int gapLength = 4)
        {
            DrawDashedLine(pixels, x0, y0, x1, y1, width, height, color, dashLength, gapLength);
        }

        public void DrawCircle(int cx, int cy, int radius, Color32 color, int thickness = 1)
        {
            DrawCircle(pixels, cx, cy, radius, width, height, color, thickness);
        }

        public void DrawRect(int x0, int y0, int w0, int h0, Color32 color)
        {
            DrawRect(pixels, x0, y0, width, height, w0, h0, color);
        }

        public void DrawRectCenter(int x0, int y0, int w0, int h0, Color32 color)
        {
            DrawRectCenter(pixels, x0, y0, width, height, w0, h0, color);
        }

        public void FillRect(int x0, int y0, int w0, int h0, Color32 color)
        {
            FillRect(pixels, x0, y0, w0, h0, width, height, color);
        }

        public void DrawRect(int x0, int y0, int w0, int h0, int thickness, Color32 color)
        {
            DrawRect(pixels, x0, y0, w0, h0, width, height, thickness, color);
        }

        public void DrawTexture(Texture2D source, Rect targetRect)
        {
            DrawTexture(pixels, source, targetRect, width, height);
        }

        public static void DrawRect(Color32[] canvas, int x0, int y0, int width, int height, int w0, int h0, Color32 color)
        {
            for (int y = y0; y < y0 + h0; y++)
            {
                if (y < 0 || y > height)
                    continue;

                for (int x = x0; x < x0 + w0; x++)
                {
                    if (x < 0 || x > width)
                        continue;

                    if (y * width + x < canvas.Length)
                    {
                        canvas[y * width + x] = color;
                    }
                }
            }
        }
        
        public static void DrawRectCenter(Color32[] canvas, int x0, int y0, int width, int height, int w0, int h0, Color32 color)
        {
            for(int y = y0 - h0; y < y0 + h0; y++)
            {
                if (y < 0 || y > height)
                    continue;

                for(int x = x0 - w0; x < x0 + w0; x++)
                {
                    if (x < 0 || x > width)
                        continue;

                    if (y * width + x < canvas.Length)
                    {
                        canvas[y * width + x] = color;
                    }
                }
            }
        }

        public static void DrawDashedLine(Color32[] canvas, int x0, int y0, int x1, int y1, int width, int height, Color32 color, int dashLength = 6, int gapLength = 4)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int step = 0;          // Total step counter
            int cycle = dashLength + gapLength;
            bool drawing = true;   // Whether a solid dash is currently being drawn

            while (true)
            {
                // Decide from the step count whether to plot the current pixel
                if (drawing)
                {
                    if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                        canvas[y0 * width + x0] = color;
                }

                // Reached the end point
                if (x0 == x1 && y0 == y1) break;

                // Step
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }

                step++;

                // Toggle the drawing state
                if (step >= (drawing ? dashLength : gapLength))
                {
                    drawing = !drawing;
                    step = 0;
                }
            }
        }

        public static void DrawCircle(Color32[] canvas,int cx, int cy, int radius, int width, int height, Color32 color, int thickness = 1)
        {
            int outerR2 = (radius + thickness / 2) * (radius + thickness / 2);
            int innerR2 = (radius - thickness / 2) * (radius - thickness / 2);
            int minX = Mathf.Clamp(cx - radius - thickness, 0, width - 1);
            int maxX = Mathf.Clamp(cx + radius + thickness, 0, width - 1);
            int minY = Mathf.Clamp(cy - radius - thickness, 0, height - 1);
            int maxY = Mathf.Clamp(cy + radius + thickness, 0, height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - cy;
                int dy2 = dy * dy;
                int rowOffset = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - cx;
                    int dist2 = dx * dx + dy2;
                    if (dist2 <= outerR2 && dist2 >= innerR2)
                    {
                        canvas[rowOffset + x] = color;
                    }
                }
            }
        }

        // Written by ds; it works, and it works well
        public static void DrawLine(Color32[] canvas, int x0, int y0, int x1, int y1, int width, int height, Color32 color)
        {
            int w = width;  // texture width
            int h = height; // texture height

            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int e2;

            while (true)
            {
                // Plot the point (with bounds check)
                if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
                    canvas[y0 * w + x0] = color;

                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void DrawTexture(Color32[] canvas, Texture2D source, Rect targetRect, int width, int height)
        {
            // Read all pixels of the source texture
            UnityEngine.Color[] srcPixels = source.GetPixels();

            int srcWidth = source.width;
            int srcHeight = source.height;

            // Compute the destination region
            int startX = Mathf.Max(0, (int)targetRect.x);
            int startY = Mathf.Max(0, (int)targetRect.y);
            int endX = Mathf.Min(width, (int)(targetRect.x + targetRect.width));
            int endY = Mathf.Min(height, (int)(targetRect.y + targetRect.height));

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // Map the destination pixel coordinates to source texture coordinates
                    float u = (x - targetRect.x) / targetRect.width;
                    float v = (y - targetRect.y) / targetRect.height;

                    int srcX = Mathf.Clamp((int)(u * srcWidth), 0, srcWidth - 1);
                    int srcY = Mathf.Clamp((int)(v * srcHeight), 0, srcHeight - 1);

                    UnityEngine.Color srcColor = srcPixels[srcY * srcWidth + srcX];
                    canvas[y * width + x] = srcColor;
                }
            }
        }

        /// <summary>
        /// Draw a filled rectangle (internal use).
        /// </summary>
        public static void FillRect(Color32[] canvas, int x, int y, int w, int h, int canvasW, int canvasH, Color32 color)
        {
            // Clip
            int sx = Mathf.Max(0, x);
            int sy = Mathf.Max(0, y);
            int ex = Mathf.Min(canvasW, x + w);
            int ey = Mathf.Min(canvasH, y + h);
            for (int py = sy; py < ey; py++)
            {
                int row = py * canvasW;
                for (int px = sx; px < ex; px++)
                    if(row + px < canvas.Length)
                        canvas[row + px] = color;
            }
        }

        /// <summary>
        /// Draw a rectangle border with thickness (keeping the original parameter signature).
        /// </summary>
        public static void DrawRect(Color32[] canvas, int x0, int y0, int width, int height, int w0, int h0, int thickness, Color32 color)
        {
            thickness = Mathf.Clamp(thickness, 1, Mathf.Min(width, height) / 2);

            // Four sides
            FillRect(canvas, x0, y0, width, thickness, w0, h0, color); // top
            FillRect(canvas, x0, y0 + height - thickness, width, thickness, w0, h0, color); // bottom
            FillRect(canvas, x0, y0 + thickness, thickness, height - 2 * thickness, w0, h0, color); // left
            FillRect(canvas, x0 + width - thickness, y0 + thickness, thickness, height - 2 * thickness, w0, h0, color); // right
        }

        public static int PixelIndex(int x, int y, int width, int height)
        {
            if(x > width)
            {
                y += x / width;
                x %= width;
            }

            if (y > height)
            {
                //what the fuck!?
                throw new ArgumentOutOfRangeException("Argument 'y' must smaller than the image height");
            }

            return y * height + x;
        }
        */
    }
}
