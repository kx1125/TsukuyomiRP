using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    // One immutable light selection per camera invocation, shared with the RT shadow producer.
    internal sealed class RectLightFrameData : ContextItem
    {
        internal bool Prepared;
        internal TsukuyomiRectLight[] Lights = System.Array.Empty<TsukuyomiRectLight>();
        internal readonly Dictionary<ulong, int> ShadowSlots = new();
        public override void Reset() { Prepared = false; Lights = System.Array.Empty<TsukuyomiRectLight>(); ShadowSlots.Clear(); }
    }

    [System.Serializable]
    internal sealed class TsukuyomiRectLightPass : GraphFeaturePass
    {
        internal const string BufferKey = "Tsukuyomi Rect Lights";
        internal static readonly int CountId = Shader.PropertyToID("_TsukuyomiRectLightCount");
        internal static readonly int HighId = Shader.PropertyToID("_TsukuyomiRectLightHasHigh");
        internal static readonly int BufferId = Shader.PropertyToID("_TsukuyomiRectLights");
        internal static readonly int[] TextureIds = {
            Shader.PropertyToID("_TsukuyomiRectGGXMatrix"), Shader.PropertyToID("_TsukuyomiRectGGXAmplitude"),
            Shader.PropertyToID("_TsukuyomiRectDisneyMatrix"), Shader.PropertyToID("_TsukuyomiRectDisneyFGD") };
        private static readonly ProfilingSampler Sampler = new("Tsukuyomi Rect Light Upload");
        private static GraphicsBuffer s_EmptyBuffer;
        private static bool s_ShuttingDown;
        private readonly List<TsukuyomiRectLight> _visible = new();
        private readonly Plane[] _planes = new Plane[6];
        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiRectLightResources _resources;
        private bool _unsupported, _warnedMissing, _warnedOverflow;
        public override string Name => "Tsukuyomi Rect Lights";
        public override void CollectTextureSlots(List<TextureSlot> slots) { }

        public void Configure(TsukuyomiPipelineProfile profile, bool unsupported)
        { _profile = profile; _unsupported = unsupported; }

        public static void ClearGlobals()
        {
            Shader.SetGlobalInt(CountId, 0);
            Shader.SetGlobalInt(HighId, 0);
            // D3D12 requires a valid SRV even when a dynamic loop executes zero times.
            if (s_ShuttingDown || SystemInfo.graphicsShaderLevel < 45) return;
            if (s_EmptyBuffer == null || !s_EmptyBuffer.IsValid())
            {
                s_EmptyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, RectLightGpuData.Stride);
                s_EmptyBuffer.SetData(new RectLightGpuData[1]);
            }
            Shader.SetGlobalBuffer(BufferId, s_EmptyBuffer);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            ReleaseFallback();
            s_ShuttingDown = false;
            ClearGlobals();
#if UNITY_EDITOR
            // Leaving Play mode raises Application.quitting, but Editor cameras
            // continue rendering and still need the empty structured buffer.
            Application.quitting -= ReleaseFallback;
            UnityEditor.EditorApplication.quitting -= ReleaseFallback;
            UnityEditor.EditorApplication.quitting += ReleaseFallback;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseFallback;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseFallback;
#else
            Application.quitting -= ReleaseFallback;
            Application.quitting += ReleaseFallback;
#endif
            // Cameras without this renderer feature must never inherit the preceding camera's list.
            RenderPipelineManager.beginCameraRendering -= ClearCamera;
            RenderPipelineManager.beginCameraRendering += ClearCamera;
            RenderPipelineManager.endCameraRendering -= ClearCamera;
            RenderPipelineManager.endCameraRendering += ClearCamera;
        }
#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Editor cameras can draw between ExitingPlayMode and EnteredEditMode.
            // Keep the fallback alive throughout the transition, including when
            // domain reload is disabled. Shutdown/reinitialization handles disposal.
            s_ShuttingDown = false;
            ClearGlobals();
        }
#endif
        private static void ClearCamera(ScriptableRenderContext context, Camera camera) => ClearGlobals();
        private static void ReleaseFallback()
        {
            s_ShuttingDown = true;
            s_EmptyBuffer?.Dispose();
            s_EmptyBuffer = null;
        }

        private sealed class UploadData
        {
            public readonly RectLightGpuData[] Lights = new RectLightGpuData[64];
            public readonly TextureHandle[] Textures = new TextureHandle[4];
            public BufferHandle Buffer;
            public int Count, HasHigh;
        }

        internal RectLightFrameData PrepareFrame(in FeatureGraphContext context)
        {
            var frame = context.FrameData.GetOrCreate<RectLightFrameData>();
            if (frame.Prepared) return frame;
            frame.Prepared = true;
            var camera = context.CameraData.camera;
            var volume = VolumeManager.instance.stack?.GetComponent<TsukuyomiRectLightVolume>();
            bool enabled = _profile && (volume != null && volume.active && volume.enable.overrideState
                ? volume.enable.value : _profile.EnableRectLights);
            int budget = Mathf.Clamp(volume != null && volume.active && volume.maxLights.overrideState
                ? volume.maxLights.value : _profile ? _profile.MaxRectLights : 16, 1, 64);
            enabled &= !_unsupported && camera && (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
                && context.CameraData.cameraTargetDescriptor.dimension == TextureDimension.Tex2D
                && SystemInfo.graphicsShaderLevel >= 45;
            var resources = enabled ? TsukuyomiRenderPipelineResourcesProvider.Current?.RectLightResources : null;
            if (_resources != resources)
            {
                if (_resources) _resources.Release();
                _resources = resources;
                if (_resources) _resources.Acquire();
            }
            if (enabled && !resources && !_warnedMissing)
            {
                Debug.LogWarning("Tsukuyomi Rect Light: assign RectLightResources in the preloaded pipeline resources; contribution disabled.");
                _warnedMissing = true;
            }
            if (resources) _warnedMissing = false;
            enabled &= resources && resources.EnsureLoaded();
            _visible.Clear();
            if (enabled)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _planes);
                foreach (var light in TsukuyomiRectLight.ActiveLights)
                {
                    if (!light || !light.IsUsable || (camera.cullingMask & (1 << light.gameObject.layer)) == 0) continue;
                    // The same centre-distance cutoff is used by every evaluator.
                    var bounds = new Bounds(light.transform.position, Vector3.one * (2 * light.Range));
                    if (GeometryUtility.TestPlanesAABB(_planes, bounds)) _visible.Add(light);
                }
                var origin = camera.transform.position;
                _visible.Sort((a, b) => {
                    int distance = (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude);
                    return distance != 0 ? distance : EntityId.ToULong(a.GetEntityId()).CompareTo(EntityId.ToULong(b.GetEntityId()));
                });
                if (_visible.Count > budget && !_warnedOverflow)
                {
                    Debug.LogWarning($"Tsukuyomi Rect Light: {_visible.Count} visible lights exceed budget {budget}; keeping nearest lights.", camera);
                    _warnedOverflow = true;
                }
                if (_visible.Count <= budget) _warnedOverflow = false;
            }
            int count = Mathf.Min(_visible.Count, budget);
            frame.Lights = new TsukuyomiRectLight[count];
            for (int i = 0; i < count; i++) frame.Lights[i] = _visible[i];
            return frame;
        }

        public override void RecordGraph(in FeatureGraphContext context)
        {
            var frame = PrepareFrame(context);
            int count = frame.Lights.Length;
            if (count == 0)
            {
                context.ResourceHub.ReleaseBuffer(BufferKey);
                ClearGlobals();
                // Unsupported devices and shutdown do not allocate fallback buffers.
                if (s_EmptyBuffer == null || !s_EmptyBuffer.IsValid()) return;
            }
            using var node = context.AddUnsafe<UploadData>(Name, Sampler);
            node.Builder.AllowGlobalStateModification(true);
            node.Builder.AllowPassCulling(false);
            var data = node.Data;
            data.Count = count;
            data.HasHigh = 0;
            // A dynamically empty light loop still requires a valid D3D12 SRV.
            // Bind it in this camera's command stream, including after releasing its old buffer.
            data.Buffer = context.RenderGraph.ImportBuffer(count > 0
                ? context.ResourceHub.GetOrCreateBuffer(BufferKey, 64, RectLightGpuData.Stride)
                : s_EmptyBuffer);
            node.Builder.UseBuffer(data.Buffer, count > 0 ? AccessFlags.Write : AccessFlags.Read);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    data.Lights[i] = frame.Lights[i].ToGpuData();
                    if (frame.ShadowSlots.TryGetValue(EntityId.ToULong(frame.Lights[i].GetEntityId()), out int slot))
                        data.Lights[i].ShadowIndex = (uint)slot;
                    if (data.Lights[i].Quality == 2) data.HasHigh = 1;
                }
                for (int i = 0; i < 4; i++)
                {
                    data.Textures[i] = context.RenderGraph.ImportTexture(_resources.Handles[i]);
                    node.Builder.UseTexture(data.Textures[i], AccessFlags.Read);
                }
            }
            node.SetRenderFunc(static (state, ctx) => {
                ctx.cmd.SetGlobalInt(CountId, state.Count);
                ctx.cmd.SetGlobalInt(HighId, state.HasHigh);
                ctx.cmd.SetGlobalBuffer(BufferId, state.Buffer);
                if (state.Count == 0) return;
                ctx.cmd.SetBufferData(state.Buffer, state.Lights);
                for (int i = 0; i < 4; i++) ctx.cmd.SetGlobalTexture(TextureIds[i], state.Textures[i]);
            });
        }

        public void Dispose()
        {
            ClearGlobals();
            if (_resources) _resources.Release();
            _resources = null;
            _visible.Clear();
        }
    }
}
