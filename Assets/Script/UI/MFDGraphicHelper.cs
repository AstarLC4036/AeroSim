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
    /// 混合模式。写进 DrawCommand.layer（这个字段以前 C# 写了但 shader 从没读过，所以拿来复用，结构体布局不变）。
    /// </summary>
    public enum MFDBlend
    {
        /// <summary>正常 alpha 混合（默认，行为不变）。</summary>
        Over = 0,
        /// <summary>destination-out：只削减已画内容的 alpha，不写入颜色 —— 用来"抠掉"一块区域。</summary>
        Erase = 1,
        /// <summary>直接覆盖，不做混合（alpha=1 时等于用该颜色顶掉已有像素）。</summary>
        Replace = 2,
        /// <summary>
        /// "形状以外"生效的擦除：和 Erase 同一套数学，但覆盖率取反。
        /// 用途是把已画内容**裁剪到某个形状里**（先大范围画，再用它擦掉形状外的部分）。
        /// 注意：擦完形状外会变成**透明**（露出后面的黑底）—— 想保留背景色请用 <see cref="FillOutsideDisc"/> 或 OverOutside。
        /// 只有 3/4/5 这三个值有"形状以外"的语义，其它 layer 值（包括误传的 int）都按 0 处理，不会突然生效。
        /// </summary>
        EraseOutside = 3,
        /// <summary>
        /// 在形状**以外**做正常 alpha 覆盖（和 Over 同一套数学，覆盖率取反）。
        /// 典型用途：姿态球画完天地之后，把圆外刷回 MFD 的灰背景色 ✓ 任何图元都能这么用。
        /// </summary>
        OverOutside = 4,
        /// <summary>在形状**以外**直接覆盖，不做混合（Replace 的反相版）。</summary>
        ReplaceOutside = 5,
    }

    /// <summary>文字的水平锚点：x 指的是文字的哪一边。Default = 用 MFDGraphicHelper.DefaultAnchor。</summary>
    public enum MFDTextAnchor
    {
        Default = 0,
        /// <summary>x 是文字左端（默认）。</summary>
        Left = 1,
        /// <summary>x 是文字水平中心（右对齐时把同一个 x 传进来，两段文字就能对齐在同一条右边界上）。</summary>
        Center = 2,
        /// <summary>x 是文字右端。</summary>
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
            public int layer;       // 混合模式，见 MFDBlend（0=Over 1=Erase 2=Replace）。绘制顺序 = 命令顺序
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

        // ================= 设计分辨率缩放 =================
        // 页面按 designSize 写坐标/尺寸/字号，实际画布（构造时的 width/height）自动缩放。
        // 默认 (0,0) = 关闭 → 所有参数 ×1，输出与加这个功能之前逐像素一致。
        private Vector2Int designSize = Vector2Int.zero;

        /// <summary>当前设计分辨率；(0,0) 表示未启用缩放。</summary>
        public Vector2Int DesignSize => designSize;

        /// <summary>设备像素 / 设计单位。未启用缩放时恒为 1。</summary>
        public float DesignScale
        {
            get
            {
                if (designSize.x <= 0 || designSize.y <= 0 || width <= 0 || height <= 0) return 1f;
                // 取两轴中较小的缩放比：万一宽高比不一致，宁可留白，也不会把内容裁掉
                return Mathf.Min((float)width / designSize.x, (float)height / designSize.y);
            }
        }

        /// <summary>
        /// 设定设计分辨率：页面按这个尺寸写坐标与字号，画布分辨率可以随时改（宽高比要和画布一致）。
        /// 传 (0,0) 关闭缩放。返回的水平推进量仍以设计单位计。
        /// </summary>
        public void SetDesignSize(int w, int h) { designSize = new Vector2Int(w, h); }
        public void SetDesignSize(Vector2Int size) { designSize = size; }

        // 设计单位 → 设备像素（关闭缩放时 ×1）
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
        /// 带线宽的重载，width 是**像素直径**（1 = 原来的细线，<=1 时观感与原来完全一致）。
        /// 本质是胶囊 SDF，所以两个端点自动是圆头，拐角处把宽线的两端重叠画即可自然接上。
        /// </summary>
        public void DrawLine(Vector2 a, Vector2 b, Color color, float width, int layer = 0)
        {
            DrawLine(a.x, a.y, b.x, b.y, color, width, layer);
        }

        /// <summary>带线宽的重载（见上）。width 用 float 传就不和 <c>int layer</c> 那个重载歧义。</summary>
        public void DrawLine(float x0, float y0, float x1, float y1, Color color, float width, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 0,
                param1 = new Vector4(Scale(x0), Scale(y0), Scale(x1), Scale(y1)),
                param2 = new Vector4(Scale(width), 0, 0, 0),   // param2 之前对 Line 是空的，正好拿来放线宽
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
        /// 实心圆（disc）。param1 = 圆心 + 半径，抗锯齿和圆环同一套（1px）。
        /// 配合 <see cref="MFDBlend.Erase"/> 就是"圆形挖洞" —— 画姿态球、把矩形裁成圆、瞄准光环遮罩都用得上。
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

        /// <summary>实心圆擦除（destination-out，不混合）：alpha=1 完全挖穿，0.5 只挖一半。</summary>
        public void DrawDiscErase(float x0, float y0, float radius, float alpha = 1f)
        {
            DrawDisc(x0, y0, radius, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        /// <summary>
        /// 只保留圆内的内容：把圆**以外**全部擦掉。等价于"裁剪到圆形"（stencil）。
        /// 用法：先随便大范围画，然后调一次这个，超出的部分就没了。
        /// </summary>
        public void DrawDiscStencil(float x0, float y0, float radius, float alpha = 1f)
        {
            DrawDisc(x0, y0, radius, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.EraseOutside);
        }

        /// <summary>
        /// 把圆**以外**刷成指定颜色（正常 alpha 覆盖）——<see cref="DrawDiscStencil"/> 的"不露黑底"版本。
        /// 姿态球的标准用法：画完天/地（会溢出）之后调一次，圆外就恢复成 MFD 背景色 ✓
        /// 其它形状同理：<c>DrawRectFill(..., (int)MFDBlend.OverOutside)</c> 就是把矩形外刷掉。
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
        /// 圆弧：只在一个角度范围内填充的**环带**（转速表那种绿弧）。
        /// 角度用度，0° = +X 方向，逆时针为正（和 Mathf.Cos/Sin 一致）✓
        /// endDeg &lt; startDeg 就是顺时针扫 ✓ 角度边缘是硬边（径向仍然是 1px 抗锯齿 ✓）
        /// thickness &lt;= 0 时变成**扇形**（从圆心一直填到半径）✓
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

        /// <summary>按 0~1 的值填一段弧（仪表盘最常用）：value=0 时画空、1 时画满整段 ✓</summary>
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

        /// <summary>扇形（从圆心填到半径，thickness 传 0 的封装 ✓）。</summary>
        public void DrawSector(Vector2 center, float radius, float startDeg, float endDeg,
                               Color color, int layer = 0)
        {
            DrawArc(center.x, center.y, radius, 0f, startDeg, endDeg, color, layer);
        }

        // ================= 三角形 / 任意多边形（path 填充） =================

        /// <summary>
        /// 实心三角形（1px 抗锯齿）。绕向无所谓，内部会自动统一成逆时针。
        /// 这是 <see cref="FillPolygon"/> 的基础图元，也可以直接用来画三角符号。
        /// </summary>
        public void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color, int layer = 0)
        {
            // 统一成逆时针（shader 里的半平面 SDF 依赖绕向）
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
        /// 用一串点连成闭合路径并**填充内部**（凸的、凹的都能处理：内部做耳切三角化）。
        /// 返回生成了多少个三角形命令（= 点数 - 2；自交/退化多边形会提前收手）。
        /// 注意：每次调用会分配少量临时 List；每帧画很多大多边形的话建议缓存顶点数组再来。
        /// 小技巧：填完再补一圈 <see cref="DrawPolygonOutline"/> 可以盖掉三角形之间的 AA 接缝。
        /// </summary>
        public int FillPolygon(IList<Vector2> points, Color color, int layer = 0)
        {
            if (points == null || points.Count < 3) return 0;

            // 有符号面积 → 决定索引顺序（统一成逆时针）
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
            int guard = idx.Count * idx.Count + 8;      // 防止退化输入死循环

            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = points[i0], b = points[i1], c = points[i2];

                    // 逆时针下 cross > 0 才是凸耳
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
                if (!clipped) break;    // 自交或退化：剩下的放弃，避免死循环
            }

            if (idx.Count == 3)
            {
                DrawTriangle(points[idx[0]], points[idx[1]], points[idx[2]], color, layer);
                made++;
            }
            return made;
        }

        /// <summary>把一串点连成线（默认闭合），用作轮廓。width &lt;= 1 时就是原来的 1px 细线。</summary>
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
        /// 设置裁剪矩形：之后的绘制**只在矩形内生效**（等于 scissor）。
        /// 参数和 <see cref="DrawRectFill(float,float,float,float,Color,int)"/> 一致：(x0,y0) 是角，w/h 是完整宽高。
        /// 一页里想做好几个各自裁剪的区域就靠它；用完记得 <see cref="ClearClip"/>（每帧命令流清空时也会自动恢复）。
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

        /// <summary>取消裁剪，恢复整屏。</summary>
        public void ClearClip()
        {
            commands.Add(new DrawCommand { type = 10, layer = 0 });
        }

        /// <summary>
        /// 旋转实心矩形：中心 + 半尺寸 + 旋转角（度）。旋转以 (cos, sin) 传进 param2，shader 每像素不用三角函数。
        /// 配合 <see cref="MFDBlend.Erase"/> 可以擦一个斜的矩形；配合 EraseOutside 可以做斜的裁剪框。
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

        /// <summary>旋转实心矩形擦除。</summary>
        public void DrawRotatedRectErase(Vector2 center, Vector2 halfSize, float angleDeg, float alpha = 1f)
        {
            DrawRotatedRectFill(center, halfSize, angleDeg, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        /// <summary>
        /// 半平面填充：(x0, y0) 是分界线上的一个点，angleDeg 是分界线的倾角，
        /// 填充**法线负侧**（局部 -Y 那一边，也就是"线下方"）。姿态球的天地分割就用这个：
        /// 分界点随俯仰上下移、倾角随滚转反向转，地下半片就跟着对上了。
        /// </summary>
        public void DrawHalfPlane(float x0, float y0, float angleDeg, float size, Color color, int layer = 0)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            // 局部 -Y 方向 = (s, -c)；把矩形中心推到界线的一侧，让它的上边正好压在分界线上
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
        /// 把一块矩形区域从已画内容里"抠掉"（destination-out，不做 alpha 混合）。
        /// 参数与 <see cref="DrawRectFill(float,float,float,float,Color,int)"/> 一致：(x0,y0) 是角，w/h 是完整宽高。
        /// 顺序很关键：必须先画内容、再擦除（每帧画布内容是按命令顺序累加的）。
        /// alpha=1 完全挖穿，0.5 只擦掉一半 alpha。
        /// </summary>
        public void DrawRectErase(float x0, float y0, float w0, float h0, float alpha = 1f)
        {
            DrawRectFill(x0, y0, w0, h0, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        /// <summary>擦除（中心 + 半宽半高版本，参数与 <see cref="DrawRectFillCenter(float,float,float,float,Color,int)"/> 一致）。</summary>
        public void DrawRectEraseCenter(float cx, float cy, float halfW, float halfH, float alpha = 1f)
        {
            DrawRectFillCenter(cx, cy, halfW, halfH, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)), (int)MFDBlend.Erase);
        }

        public void Submit()
        {
            if (!registered)
            {
                RegisterHelper();      // 兜底：退出时统一释放（即使 owner 忘了 Dispose）
            }

            mfdCompute.SetTexture(kernelIndex, "_Result", outputRT);
            mfdCompute.SetInt("_Width", width);
            mfdCompute.SetInt("_Height", height);
            mfdCompute.SetTexture(kernelIndex, "_SourceTex", outputRT);

            if (commands.Count == 0) return;

            if (commandBuffer == null || commandBuffer.count < commands.Count)
            {
                commandBuffer?.Release();
                // 容量按 2 的幂增长，避免命令数逐条增长时反复重建 native buffer
                int capacity = Mathf.Max(Mathf.NextPowerOfTwo(Mathf.Max(commands.Count, 1)), 64);
                commandBuffer = new ComputeBuffer(capacity,
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(DrawCommand)));
            }

            commandBuffer.SetData(commands);
            mfdCompute.SetBuffer(kernelIndex, "_Commands", commandBuffer);
            mfdCompute.SetInt("_CommandCount", commands.Count);

            // 文字字体图集（只有画了 type==6 才真正用到）
            TMP_FontAsset font = TextFont;
            if (font != null && font.atlasTexture != null)
            {
                mfdCompute.SetTexture(kernelIndex, "_FontAtlas", font.atlasTexture);
                mfdCompute.SetInt("_FontAtlasChannel",
                    FontAtlasChannelOverride >= 0 ? FontAtlasChannelOverride : AtlasChannel(font));
            }
            else
            {
                // 兜底：避免 _FontAtlas 悬空（D3D 下未绑定 SRV 采样会刷 NULL SRV 警告）
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
            liveHelpers.Clear();                 // 关掉 Domain Reload 时静态字段会跨播放残留
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

        // ===================== 文字绘制（TMP SDF 图集，单次 Dispatch） =====================
        public const float DefaultTextSharpness = 0.1f;

        /// <summary>
        /// 图集 glyphRect.y 是否以"顶部"为原点。TMP 官方是左下原点（见 TMP_Text.cs 里 vertex_BL.uv = glyphRect.y/H），
        /// 所以默认 false。若某个字体画出来是空白/错位，可以先翻一下这个开关。
        /// </summary>
        public static bool FlipGlyphV = false;

        /// <summary>是否把 SDF 图集的 padding 边距也画进四边形（TMP 的做法，边缘更柔和完整）。</summary>
        public static bool TextUseAtlasPadding = true;

        /// <summary>调试用：>=0 时强制覆盖图集通道。0=alpha, 1=red, 2=整块实心（确认四边形位置）。</summary>
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

        /// <summary>最后一道兜底：Resources 里的 MFD/HUD 字体，保证不配置也能画字。</summary>
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

        /// <summary>本绘制器使用的字体；未设置时回退到 MFDGraphicHelperSettings.defaultFont，再回退到 Resources 默认字体。</summary>
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
        /// 字距（tracking）：每个字符之间额外增加的间距，单位是**字号的比例**（em）。
        /// 0.05 = 每个字之间多空 5% 字号；负值收紧。0 就是字体本身的 advance。
        /// 只加在"字与字之间"，所以 MeasureText / DrawTextCentered 的宽度与居中都是对的。
        /// </summary>
        public float LetterSpacing = 0f;

        /// <summary>DrawText 不显式给 anchor 时用的默认锚点（只影响水平方向）。</summary>
        public MFDTextAnchor DefaultAnchor = MFDTextAnchor.Left;

        /// <summary>
        /// 在 (x, y) 画一行文字。坐标系与其它 Draw* 一致：y 向上，(x, y) 是该行 **baseline** 的起点。
        /// anchor 决定 x 指的是文字的哪一边：Left（默认，x=左端）/ Center（x=水平中心）/ Right（x=右端）。
        /// 返回水平推进量（像素），方便接着画下一段。
        /// </summary>
        public float DrawText(string text, float x, float y, Color color,
                              float size = 16f, float sharpness = DefaultTextSharpness,
                              MFDTextAnchor anchor = MFDTextAnchor.Default,
                              float letterSpacing = float.NaN)
        {
            return LayoutText(text, Scale(x), Scale(y), color, Scale(size), sharpness,
                              ResolveAnchor(anchor), false, 0f, letterSpacing) / DesignScale;
        }

        /// <summary>同上（位置参数版，第 7 个参数是字距）。anchor 用 <see cref="DefaultAnchor"/>。</summary>
        public float DrawText(string text, float x, float y, Color color,
                              float size, float sharpness, float tracking)
        {
            return LayoutText(text, Scale(x), Scale(y), color, Scale(size), sharpness,
                              ResolveAnchor(MFDTextAnchor.Default), false, 0f, tracking) / DesignScale;
        }

        /// <summary>以 (cx, cy) 为中心画一行文字（水平居中，垂直按大写字高居中）。</summary>
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

        /// <summary>只量宽度（像素），不产生绘制命令。用当前的 <see cref="LetterSpacing"/>。</summary>
        public float MeasureText(string text, float size)
        {
            return MeasureText(text, size, LetterSpacing);
        }

        /// <summary>只量宽度（设计单位）。tracking 与 DrawText 的 letterSpacing 同义。</summary>
        public float MeasureText(string text, float size, float letterSpacing)
        {
            return MeasureTextDevice(text, Scale(size), letterSpacing) / DesignScale;
        }

        // 设备像素空间的测量（内部用：LayoutText 已经在设备空间里工作）
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
                if (i < text.Length - 1) w += tracking;      // 字距只算字与字之间
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

            // 动态字体：字形可能还没进图集（characterLookupTable 里查不到 → 静默画不出来）
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                font.TryAddCharacters(text, out string _);

            if (font.characterLookupTable == null || font.atlasTexture == null) return 0f;

            Texture atlas = font.atlasTexture;
            float atlasW = atlas.width;
            float atlasH = atlas.height;
            float px = size / font.faceInfo.pointSize * font.faceInfo.scale;
            int sharp = Mathf.Clamp(Mathf.RoundToInt(sharpness * 1000f), 1, 8000);

            float lineWidth = MeasureTextDevice(text, size, letterSpacing);
            float tracking = letterSpacing * size;          // 像素

            // 水平锚点：x 指左端 / 中心 / 右端
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
                        // 目标矩形（像素对齐是清晰的关键）
                        float x0 = Mathf.Round(penX + glyph.metrics.horizontalBearingX * px);
                        float x1 = Mathf.Round(penX + (glyph.metrics.horizontalBearingX + r.width) * px);
                        float y1 = Mathf.Round(baseline + glyph.metrics.horizontalBearingY * px);   // 顶
                        float y0 = Mathf.Round(y1 - r.height * px);                                  // 底

                        float u0 = r.x / atlasW;
                        float u1 = (r.x + r.width) / atlasW;
                        float vA = r.y / atlasH;                        // glyphRect 底边（TMP：左下原点）
                        float vB = (r.y + r.height) / atlasH;           // glyphRect 顶边
                        float v0 = FlipGlyphV ? 1f - vB : vA;
                        float v1 = FlipGlyphV ? 1f - vA : vB;

                        // SDF padding：把图集里字形周围的距离场边距一起采样/一起画，边缘过渡才完整
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
                            dashLength = sharp,     // 复用为 sharpness*1000（保持 DrawCommand 布局不变）
                            gapLength = 0,
                            layer = 0
                        });
                    }

                    penX += glyph.metrics.horizontalAdvance * px;
                }

                if (i < text.Length - 1) penX += tracking;      // 字距只加在字与字之间（和 MeasureText 一致）
            }
            return lineWidth;
        }

        /// <summary>TMP 图集里 SDF 值所在通道：Alpha8 → alpha(0)，R8/R16 → red(1)。</summary>
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
            int step = 0;          // 总步数计数器
            int cycle = dashLength + gapLength;
            bool drawing = true;   // 当前是否在画实线段

            while (true)
            {
                // 根据步数决定是否绘制当前像素
                if (drawing)
                {
                    if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                        canvas[y0 * width + x0] = color;
                }

                // 到达终点
                if (x0 == x1 && y0 == y1) break;

                // 步进
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }

                step++;

                // 切换绘制状态
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

        //这是ds写的，能用，很好
        public static void DrawLine(Color32[] canvas, int x0, int y0, int x1, int y1, int width, int height, Color32 color)
        {
            int w = width;  // 纹理宽度
            int h = height; // 纹理高度

            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int e2;

            while (true)
            {
                // 画点（带边界检查）
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
            // 读取源纹理所有像素
            UnityEngine.Color[] srcPixels = source.GetPixels();

            int srcWidth = source.width;
            int srcHeight = source.height;

            // 计算目标区域
            int startX = Mathf.Max(0, (int)targetRect.x);
            int startY = Mathf.Max(0, (int)targetRect.y);
            int endX = Mathf.Min(width, (int)(targetRect.x + targetRect.width));
            int endY = Mathf.Min(height, (int)(targetRect.y + targetRect.height));

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // 将目标像素坐标映射到源纹理坐标
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
        /// 绘制实心矩形（内部使用）。
        /// </summary>
        public static void FillRect(Color32[] canvas, int x, int y, int w, int h, int canvasW, int canvasH, Color32 color)
        {
            // 裁剪
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
        /// 绘制带厚度的矩形边框（保持原始参数签名）。
        /// </summary>
        public static void DrawRect(Color32[] canvas, int x0, int y0, int width, int height, int w0, int h0, int thickness, Color32 color)
        {
            thickness = Mathf.Clamp(thickness, 1, Mathf.Min(width, height) / 2);

            // 四边
            FillRect(canvas, x0, y0, width, thickness, w0, h0, color); // 上
            FillRect(canvas, x0, y0 + height - thickness, width, thickness, w0, h0, color); // 下
            FillRect(canvas, x0, y0 + thickness, thickness, height - 2 * thickness, w0, h0, color); // 左
            FillRect(canvas, x0 + width - thickness, y0 + thickness, thickness, height - 2 * thickness, w0, h0, color); // 右
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
