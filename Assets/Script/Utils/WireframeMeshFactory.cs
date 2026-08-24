using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Utils
{
    public class WireframeMeshFactory
    {
        public static Mesh CreateQuad(float size = 1)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-size/2, -size/2, 0),
                new Vector3(size/2, -size/2, 0),
                new Vector3(-size/2, size/2, 0),
                new Vector3(size/2, size/2, 0)
            };
            mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            return mesh;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int startIndex = vertices.Count;
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            // 两个三角形组成一个四边形
            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        /// <summary>
        /// 创建带厚度的矩形线框（世界空间，XY平面）
        /// </summary>
        /// <param name="width">矩形宽度</param>
        /// <param name="height">矩形高度</param>
        /// <param name="thickness">线框厚度</param>
        public static Mesh CreateThickRectangle(float width, float height, float thickness)
        {
            float hw = width / 2f;
            float hh = height / 2f;
            float t = thickness / 2f;

            // 四个角点（内圈）
            Vector3 bl_in = new Vector3(-hw + t, -hh + t, 0);
            Vector3 br_in = new Vector3(hw - t, -hh + t, 0);
            Vector3 tr_in = new Vector3(hw - t, hh - t, 0);
            Vector3 tl_in = new Vector3(-hw + t, hh - t, 0);

            // 四个角点（外圈）
            Vector3 bl_out = new Vector3(-hw, -hh, 0);
            Vector3 br_out = new Vector3(hw, -hh, 0);
            Vector3 tr_out = new Vector3(hw, hh, 0);
            Vector3 tl_out = new Vector3(-hw, hh, 0);

            // 顶点列表（三角形面）
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // 每条边由两个矩形组成（内外圈之间填充）
            // 下边
            AddQuad(vertices, triangles, bl_out, br_out, br_in, bl_in);
            // 右边
            AddQuad(vertices, triangles, br_out, tr_out, tr_in, br_in);
            // 上边
            AddQuad(vertices, triangles, tr_out, tl_out, tl_in, tr_in);
            // 左边
            AddQuad(vertices, triangles, tl_out, bl_out, bl_in, tl_in);

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 创建带厚度的圆环线框（世界空间，XY平面）
        /// </summary>
        /// <param name="radius">圆环半径</param>
        /// <param name="thickness">线框厚度</param>
        /// <param name="segments">圆弧分段数（越大越平滑）</param>
        public static Mesh CreateThickCircle(float radius, float thickness, int segments = 64)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            float innerRadius = radius - thickness / 2f;
            float outerRadius = radius + thickness / 2f;

            for (int i = 0; i < segments; i++)
            {
                float angle0 = 360f * i / segments * Mathf.Deg2Rad;
                float angle1 = 360f * (i + 1) / segments * Mathf.Deg2Rad;

                // 外圈点
                Vector3 outer0 = new Vector3(Mathf.Cos(angle0) * outerRadius, Mathf.Sin(angle0) * outerRadius, 0);
                Vector3 outer1 = new Vector3(Mathf.Cos(angle1) * outerRadius, Mathf.Sin(angle1) * outerRadius, 0);

                // 内圈点
                Vector3 inner0 = new Vector3(Mathf.Cos(angle0) * innerRadius, Mathf.Sin(angle0) * innerRadius, 0);
                Vector3 inner1 = new Vector3(Mathf.Cos(angle1) * innerRadius, Mathf.Sin(angle1) * innerRadius, 0);

                // 填充环形段
                AddQuad(vertices, triangles, outer0, outer1, inner1, inner0);
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
