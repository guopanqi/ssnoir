#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Theatre;
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // Geometry built once on load; UV.x stores normalized accumulated path distance for reveal.
    internal static class TheatreMesh
    {
        public static Mesh Build(TheatreNode node)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            if (node.Shape == TheatreShape.Line)
            {
                var distances = new float[node.Points.Length];
                for (int i = 1; i < distances.Length; i++) distances[i] = distances[i - 1] + Vector2.Distance(P(node.Points[i]), P(node.Points[i - 1]));
                for (int i = 1; i < distances.Length; i++)
                {
                    Vector2 a = P(node.Points[i - 1]), b = P(node.Points[i]);
                    Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * node.Width * 0.5f;
                    int start = vertices.Count;
                    vertices.Add(a - normal); vertices.Add(a + normal); vertices.Add(b + normal); vertices.Add(b - normal);
                    float from = distances[i - 1] / distances[distances.Length - 1], to = distances[i] / distances[distances.Length - 1];
                    uv.Add(new Vector2(from, 0)); uv.Add(new Vector2(from, 1)); uv.Add(new Vector2(to, 1)); uv.Add(new Vector2(to, 0));
                    Quad(triangles, start);
                    if (i < distances.Length - 1) Disc(vertices, uv, triangles, b, node.Width * 0.5f, to);
                }
            }
            else if (node.Shape == TheatreShape.Polygon)
            {
                foreach (var point in node.Points) { vertices.Add(P(point)); uv.Add(Vector2.zero); }
                for (int i = 1; i < vertices.Count - 1; i++) { triangles.Add(0); triangles.Add(i); triangles.Add(i + 1); }
            }
            else if (node.Shape == TheatreShape.Glow || node.Shape == TheatreShape.Image)
            {
                float top = node.Shape == TheatreShape.Image ? -node.Height : -node.Height * 0.5f;
                float bottom = node.Shape == TheatreShape.Image ? 0 : node.Height * 0.5f;
                vertices.Add(new Vector3(-node.Width * 0.5f, top)); vertices.Add(new Vector3(node.Width * 0.5f, top));
                vertices.Add(new Vector3(node.Width * 0.5f, bottom)); vertices.Add(new Vector3(-node.Width * 0.5f, bottom));
                uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
                Quad(triangles, 0);
            }
            else throw new ArgumentException("invisible theatre node has no mesh");
            var mesh = new Mesh { name = "Theatre/" + node.Id };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }
        private static Vector2 P(TheatrePoint p) => new(p.X, p.Y);
        private static void Quad(List<int> indices, int i) { indices.Add(i); indices.Add(i + 1); indices.Add(i + 2); indices.Add(i); indices.Add(i + 2); indices.Add(i + 3); }
        private static void Disc(List<Vector3> vertices, List<Vector2> uv, List<int> indices, Vector2 center, float radius, float distance)
        {
            int start = vertices.Count; vertices.Add(center); uv.Add(new Vector2(distance, 0.5f));
            for (int i = 0; i <= 8; i++) { float angle = i * Mathf.PI / 4f; vertices.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius); uv.Add(new Vector2(distance, 0.5f)); }
            for (int i = 1; i <= 8; i++) { indices.Add(start); indices.Add(start + i); indices.Add(start + i + 1); }
        }
    }
}
