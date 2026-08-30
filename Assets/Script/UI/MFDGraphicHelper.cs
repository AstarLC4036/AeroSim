using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace AeroSim.UI
{
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
            Texture = 5
        }

        public struct DrawCommand
        {
            public int type;
            public Vector4 param1;
            public Vector4 param2;
            public Vector4 color;
            public int dashLength;
            public int gapLength;
            public int layer;
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
                param1 = new Vector4(x0, y0, x1, y1),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawDashedLine(Vector2 a, Vector2 b, Color color, int dashedlength = 4, int gapLength = 6, int layer = 0)
        {
            DrawDashedLine(a.x, a.y, b.x, b.y, color, dashedlength, gapLength);
        }

        public void DrawDashedLine(float x0, float y0, float x1, float y1, Color color, int dashLength = 4, int gapLength = 6, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 5,
                param1 = new Vector4(x0, y0, x1, y1),
                color = new Vector4(color.r, color.g, color.b, color.a),
                dashLength = dashLength,
                gapLength = gapLength,
                layer = layer
            });
        }

        public void DrawCircle(Vector2 center, float radius, float thickness, Color color, int layer = 0)
        {
            DrawCircle(center.x, center.y, radius, thickness, color);
        }

        public void DrawCircle(float x0, float y0, float radius, float thickness, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 2,
                param1 = new Vector4(x0, y0, radius, thickness),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectOutlineCenter(Vector2 center, Vector2 size, float thickness, Color color, int layer = 0)
        {
            DrawRectOutlineCenter(center.x, center.y, size.x, size.y, thickness, color);
        }

        public void DrawRectOutlineCenter(float x0, float y0, float w0, float h0, float thickness, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 3,
                param1 = new Vector4(x0, y0, w0, h0),
                param2 = new Vector4(thickness, 0, 0, 0),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectOutline(Vector2 center, Vector2 size, float thickness, Color color, int layer = 0)
        {
            DrawRectOutline(center.x, center.y, size.x, size.y, thickness, color);
        }

        public void DrawRectOutline(float x0, float y0, float w0, float h0, float thickness, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 3,
                param1 = new Vector4(x0 + w0 / 2, y0 + h0 / 2, w0 / 2, h0 / 2),
                param2 = new Vector4(thickness, 0, 0, 0),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectFillCenter(Vector2 center, Vector2 size, Color color, int layer = 0)
        {
            DrawRectFillCenter(center.x, center.y, size.x, size.y, color);
        }

        public void DrawRectFillCenter(float x0, float y0, float w0, float h0, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 4,
                param1 = new Vector4(x0, y0, w0, h0),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void DrawRectFill(Vector2 center, Vector2 size, Color color, int layer = 0)
        {
            DrawRectFill(center.x, center.y, size.x, size.y, color);
        }

        public void DrawRectFill(float x0, float y0, float w0, float h0, Color color, int layer = 0)
        {
            commands.Add(new DrawCommand
            {
                type = 4,
                param1 = new Vector4(x0 + w0 / 2, y0 + h0 / 2, w0 / 2, h0 / 2),
                color = new Vector4(color.r, color.g, color.b, color.a),
                layer = layer
            });
        }

        public void Submit()
        {
            mfdCompute.SetTexture(kernelIndex, "_Result", outputRT);
            mfdCompute.SetInt("_Width", width);
            mfdCompute.SetInt("_Height", height);
            mfdCompute.SetTexture(kernelIndex, "_SourceTex", outputRT);

            if (commands.Count == 0) return;

            if (commandBuffer == null || commandBuffer.count < commands.Count)
            {
                commandBuffer?.Release();
                commandBuffer = new ComputeBuffer(Mathf.Max(commands.Count, 64),
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(DrawCommand)));
            }

            commandBuffer.SetData(commands);
            mfdCompute.SetBuffer(kernelIndex, "_Commands", commandBuffer);
            mfdCompute.SetInt("_CommandCount", commands.Count);
            mfdCompute.Dispatch(kernelIndex, width / 8, height / 8, 1);

            commands.Clear();
        }

        public void Dispose()
        {
            commandBuffer?.Release();
            commandBuffer = null;
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
