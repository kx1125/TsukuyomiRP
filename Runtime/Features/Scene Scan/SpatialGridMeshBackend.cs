using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal interface ISpatialGridBackend : System.IDisposable
    {
        bool Configure(Shader shader);
        void RecordGraph(in FeatureGraphContext graph, in SpatialGridParameters parameters);
    }

    // Only this backend owns geometry limits and mesh layout. Future implementations can record compute/VFX nodes.
    internal sealed class SpatialGridMeshBackend : ISpatialGridBackend
    {
        private readonly TextureSlot _color = TextureSlot.ReadWrite("Spatial Grid Color", BuiltinTexture.ActiveColor)
            .WithBinding(TextureBinding.ColorAttachment);
        private readonly TextureSlot _depth = TextureSlot.Read("Spatial Grid Depth", BuiltinTexture.CameraDepthTexture);
        private readonly TextureSlot _excluded = TextureSlot.Read("SceneScanExcludedMask", BuiltinTexture.None);
        private readonly Dictionary<int, Mesh> _meshes = new();
        private Material _material;
        private Shader _shader;
        private static readonly int DepthId = Shader.PropertyToID("_ScanGridDepth");
        private static readonly int ExcludedId = Shader.PropertyToID("_ScanGridExcluded");
        private static readonly int AnchorId = Shader.PropertyToID("_ScanGridAnchor");
        private static readonly int OriginId = Shader.PropertyToID("_ScanGridOrigin");
        private static readonly int CurvesId = Shader.PropertyToID("_ScanGridCurves");
        private static readonly int StyleId = Shader.PropertyToID("_ScanGridStyle");
        private static readonly int RangeId = Shader.PropertyToID("_ScanGridRange");
        private static readonly int ColorId = Shader.PropertyToID("_ScanGridColor");
        private static readonly int CoverageId = Shader.PropertyToID("_ScanGridCoverageMode");

        public bool Configure(Shader shader)
        {
            if (_shader != shader || !_material)
            {
                if (_material) CoreUtils.Destroy(_material);
                _shader = shader;
                _material = shader ? CoreUtils.CreateEngineMaterial(shader) : null;
            }
            return _material != null;
        }

        private sealed class GridData
        {
            public Mesh Mesh;
            public Material Material;
            public TextureHandle Depth, Excluded;
            public Vector4 Anchor, Origin, Curves, Style, Range, Color;
            public float Coverage;
        }

        public void RecordGraph(in FeatureGraphContext graph, in SpatialGridParameters parameters)
        {
            if (!_material || graph.Resources.IsActiveTargetBackBuffer) return;
            var color = graph.GetTexture(_color);
            var depth = graph.GetTexture(_depth);
            if (!color.IsValid() || !depth.IsValid()) return;
            var excluded = graph.GetTexture(_excluded);
            if (!excluded.IsValid()) excluded = graph.RenderGraph.defaultResources.blackTexture;
            float spacing = Mathf.Max(.01f, parameters.Spacing);
            float radius = Mathf.Clamp(parameters.Radius, 1f, 40f);
            int halfCount = Mathf.Clamp(Mathf.CeilToInt(radius / spacing), 1, 8);
            radius = Mathf.Min(radius, halfCount * spacing);
            Vector3 origin = parameters.OriginWS;
            Vector3 anchor = new Vector3(Mathf.Floor(origin.x / spacing + .5f),
                Mathf.Floor(origin.y / spacing + .5f), Mathf.Floor(origin.z / spacing + .5f)) * spacing;
            using var node = graph.AddRaster<GridData>("Tsukuyomi Spatial Grid Render");
            node.ColorAttachment(color, access: AccessFlags.ReadWrite);
            node.Data.Depth = node.ReadTexture(depth);
            node.Data.Excluded = node.ReadTexture(excluded);
            node.Data.Mesh = GetMesh(halfCount);
            node.Data.Material = _material;
            node.Data.Anchor = new Vector4(anchor.x, anchor.y, anchor.z, spacing);
            node.Data.Origin = new Vector4(origin.x, origin.y, origin.z, parameters.TransitionProgress);
            node.Data.Curves = new Vector4(parameters.Blend, parameters.WorldProgress, parameters.ScreenProgress, 0f);
            node.Data.Style = new Vector4(Mathf.Max(.001f, parameters.LineWidth), Mathf.Max(.005f, parameters.PointSize),
                Mathf.Max(0f, parameters.Intensity), Mathf.Max(0f, parameters.PulseSpeed));
            node.Data.Range = new Vector4(radius, Mathf.Max(.01f, parameters.MaxRange), Mathf.Max(.01f, parameters.RingWidth),
                Mathf.Max(0f, parameters.NoiseStrength));
            node.Data.Color = parameters.Color;
            node.Data.Coverage = (float)parameters.Coverage;
            node.SetRenderFunc(static (data, context) =>
            {
                data.Material.SetTexture(DepthId, data.Depth);
                data.Material.SetTexture(ExcludedId, data.Excluded);
                data.Material.SetVector(AnchorId, data.Anchor);
                data.Material.SetVector(OriginId, data.Origin);
                data.Material.SetVector(CurvesId, data.Curves);
                data.Material.SetVector(StyleId, data.Style);
                data.Material.SetVector(RangeId, data.Range);
                data.Material.SetVector(ColorId, data.Color);
                data.Material.SetFloat(CoverageId, data.Coverage);
                context.cmd.DrawMesh(data.Mesh, Matrix4x4.identity, data.Material, 0, 0);
            });
        }

        private Mesh GetMesh(int halfCount)
        {
            if (_meshes.TryGetValue(halfCount, out Mesh mesh) && mesh) return mesh;
            int side = halfCount * 2 + 1;
            int quads = side * side * side + 3 * (side - 1) * side * side;
            var vertices = new Vector3[quads * 4];
            var corners = new Vector2[quads * 4];
            var axes = new Vector3[quads * 4];
            var indices = new int[quads * 6];
            int quad = 0;
            for (int z = -halfCount; z <= halfCount; z++)
            for (int y = -halfCount; y <= halfCount; y++)
            for (int x = -halfCount; x <= halfCount; x++)
            {
                Vector3 point = new Vector3(x, y, z);
                AddQuad(point, Vector3.zero, vertices, corners, axes, indices, ref quad);
                if (x < halfCount) AddQuad(point + Vector3.right * .5f, Vector3.right, vertices, corners, axes, indices, ref quad);
                if (y < halfCount) AddQuad(point + Vector3.up * .5f, Vector3.up, vertices, corners, axes, indices, ref quad);
                if (z < halfCount) AddQuad(point + Vector3.forward * .5f, Vector3.forward, vertices, corners, axes, indices, ref quad);
            }
            mesh = new Mesh
            {
                name = $"Scene Scan Lattice {side} Cubed",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.uv = corners;
            mesh.normals = axes; // An axis attribute, not a lighting normal; zero marks billboard points.
            mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (side + 2));
            mesh.UploadMeshData(true);
            _meshes[halfCount] = mesh;
            return mesh;
        }

        private static void AddQuad(Vector3 center, Vector3 axis, Vector3[] vertices, Vector2[] corners,
            Vector3[] axes, int[] indices, ref int quad)
        {
            int v = quad * 4;
            for (int i = 0; i < 4; i++) { vertices[v + i] = center; axes[v + i] = axis; }
            corners[v] = new Vector2(-1f, -1f);
            corners[v + 1] = new Vector2(1f, -1f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(-1f, 1f);
            int t = quad * 6;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            quad++;
        }

        public void Dispose()
        {
            if (_material) CoreUtils.Destroy(_material);
            foreach (var mesh in _meshes.Values) if (mesh) CoreUtils.Destroy(mesh);
            _meshes.Clear();
            _material = null;
            _shader = null;
        }
    }
}
