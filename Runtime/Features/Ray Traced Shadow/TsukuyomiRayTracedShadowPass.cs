using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RayTracedShadowGpuLight
    {
        public Vector4 PositionRange, DirectionType, RightSize, UpSize, ShapeParams, ShadowParams;
        public const int Stride = 96;
    }

    /// <summary>Produces opaque screen-space visibility; does not replace URP's shadow globals.</summary>
    internal sealed class TsukuyomiRayTracedShadowPass : GraphFeaturePass, IDisposable
    {
        private const string HistoryA = "RT Shadow History A", HistoryB = "RT Shadow History B";
        private const string GeometryA = "RT Shadow Geometry A", GeometryB = "RT Shadow Geometry B";
        private static readonly int CountId = Shader.PropertyToID("_TsukuyomiRTShadowCount");
        private static readonly int MainId = Shader.PropertyToID("_TsukuyomiRTShadowMainLightIndex");
        private static readonly int StrengthsId = Shader.PropertyToID("_TsukuyomiRTShadowStrengths");
        private static readonly int FallbackFadeId = Shader.PropertyToID("_TsukuyomiRTShadowFallbackFade");
        private static readonly int AdditionalMapId = Shader.PropertyToID("_TsukuyomiRTShadowAdditionalLightMap");
        private static readonly int RectMapId = Shader.PropertyToID("_TsukuyomiRTShadowRectLightMap");
        private static readonly int AdditionalCountId = Shader.PropertyToID("_TsukuyomiRTShadowAdditionalLightMapCount");
        private static readonly int RectCountId = Shader.PropertyToID("_TsukuyomiRTShadowRectLightMapCount");
        private static readonly int OutputId = Shader.PropertyToID("_TsukuyomiRTShadowTexture");
        private static readonly ProfilingSampler TraceSampler = new("Tsukuyomi RT Shadow Trace");
        private static readonly ProfilingSampler DenoiseSampler = new("Tsukuyomi RT Shadow Denoise");
        private static readonly UnityEngine.Profiling.CustomSampler BuildSampler = UnityEngine.Profiling.CustomSampler.Create("Tsukuyomi RTAS Build", true);
        private static readonly UnityEngine.Profiling.CustomSampler RaysSampler = UnityEngine.Profiling.CustomSampler.Create("Tsukuyomi RT Rays", true);
        private static readonly Unity.Profiling.ProfilerMarker SceneSampler = new("Tsukuyomi RT Scene Update");
        private static GraphicsBuffer s_EmptyMap;
        private static Texture2DArray s_White;
        private static bool s_ShuttingDown;
        private static readonly HashSet<TsukuyomiRayTracedShadowPass> s_Instances = new();
        private readonly Dictionary<Camera, CameraState> _cameras = new();
        private readonly List<Camera> _retired = new();
        private readonly List<Candidate> _candidates = new();
        private TsukuyomiRectLightPass _rectLights;
        private RayTracedShadowSettings _settings;
        private RayTracingShader _trace;
        private ComputeShader _denoise;
        private bool _ready, _warnedUnsupported, _warnedResources, _warnedOverflow;
        internal bool RequiresNormals => _ready;
        public override string Name => "Tsukuyomi Ray Traced Shadows";

        public TsukuyomiRayTracedShadowPass() => s_Instances.Add(this);

        public void Configure(TsukuyomiPipelineProfile profile, bool deferred, TsukuyomiRectLightPass rectLights, Camera camera)
        {
            _rectLights = rectLights;
            _settings = RayTracedShadowSettings.Resolve(profile);
            bool supported = !deferred && (!camera || !camera.stereoEnabled) && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12
                && SystemInfo.supportsRayTracingShaders && SystemInfo.supportsComputeShaders;
            var resources = TsukuyomiRenderPipelineResourcesProvider.Current;
            var trace = resources ? resources.RayTracedShadowShader : null;
            var denoise = resources ? resources.RayTracedShadowDenoiser : null;
            if (_trace != trace || _denoise != denoise) ReleaseCameras();
            _trace = trace; _denoise = denoise;
            bool requested = _settings.Enabled && HasRequestedLights(profile);
            _ready = requested && supported && trace && denoise;
            if (requested && !supported && !_warnedUnsupported)
            {
                Debug.LogWarning("Tsukuyomi RT Shadows require DX12 ray tracing and non-XR Forward/Forward+. Existing shadows remain active.");
                _warnedUnsupported = true;
            }
            if (requested && supported && (!trace || !denoise) && !_warnedResources)
            {
                Debug.LogWarning("Tsukuyomi RT Shadows: assign the ray tracing shader and denoiser in preloaded pipeline resources.");
                _warnedResources = true;
            }
            if (supported) _warnedUnsupported = false;
            if (trace && denoise) _warnedResources = false;
            // Volume overrides belong to this camera; disabling one camera must not discard another's history.
            if (!_ready && camera && _cameras.Remove(camera, out var inactive)) inactive.Dispose();
            _retired.Clear();
            foreach (var trackedCamera in _cameras.Keys) if (!trackedCamera) _retired.Add(trackedCamera);
            foreach (var trackedCamera in _retired) { _cameras[trackedCamera].Dispose(); _cameras.Remove(trackedCamera); }
        }

        private static bool HasRequestedLights(TsukuyomiPipelineProfile profile)
        {
            foreach (var light in TsukuyomiRayTracedShadowLight.ActiveLights) if (light && light.IsUsable) return true;
            var volume = VolumeManager.instance.stack?.GetComponent<TsukuyomiRectLightVolume>();
            bool rectanglesEnabled = profile && (volume != null && volume.active && volume.enable.overrideState
                ? volume.enable.value : profile.EnableRectLights);
            if (!rectanglesEnabled) return false;
            foreach (var light in TsukuyomiRectLight.ActiveLights)
                if (light && light.IsUsable && light.ShadowMode == RayTracedShadowMode.RayTraced && light.ShadowStrength > 0) return true;
            return false;
        }

        public override void CollectTextureSlots(List<TextureSlot> slots)
        {
            if (!_ready) return;
            slots.Add(TextureSlot.Read("RT Shadow Depth", BuiltinTexture.CameraDepthTexture));
            slots.Add(TextureSlot.Read("RT Shadow Normals", BuiltinTexture.CameraNormals));
            slots.Add(TextureSlot.Read("RT Shadow Motion", BuiltinTexture.MotionVectorColor));
        }

        internal static void ClearGlobals()
        {
            Shader.SetGlobalInt(CountId, 0); Shader.SetGlobalInt(MainId, -1);
            Shader.SetGlobalInt(AdditionalCountId, 0); Shader.SetGlobalInt(RectCountId, 0);
            if (s_ShuttingDown || SystemInfo.graphicsShaderLevel < 45 || !SystemInfo.supports2DArrayTextures) return;
            if (s_EmptyMap == null || !s_EmptyMap.IsValid())
            {
                s_EmptyMap = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
                s_EmptyMap.SetData(new[] { -1 });
            }
            if (!s_White)
            {
                s_White = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false, true)
                    { name = "RT Shadow Fallback", hideFlags = HideFlags.HideAndDontSave };
                s_White.SetPixels(new[] { Color.white }, 0); s_White.Apply(false, true);
            }
            Shader.SetGlobalBuffer(AdditionalMapId, s_EmptyMap); Shader.SetGlobalBuffer(RectMapId, s_EmptyMap);
            Shader.SetGlobalTexture(OutputId, s_White);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            ReleaseAll(); s_ShuttingDown = false; ClearGlobals();
            RenderPipelineManager.beginCameraRendering -= ClearCamera;
            RenderPipelineManager.beginCameraRendering += ClearCamera;
            RenderPipelineManager.endCameraRendering -= ClearCamera;
            RenderPipelineManager.endCameraRendering += ClearCamera;
#if UNITY_EDITOR
            // Application.quitting also runs when the Editor leaves Play mode.
            // That is not an application shutdown: Scene/Game cameras keep rendering.
            Application.quitting -= ReleaseAll;
            UnityEditor.EditorApplication.quitting -= ReleaseAll;
            UnityEditor.EditorApplication.quitting += ReleaseAll;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAll;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
#else
            Application.quitting -= ReleaseAll; Application.quitting += ReleaseAll;
#endif
        }
#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Reset bindings before retiring camera resources. Editor cameras can
            // still draw during the transition, so the empty SRVs and white array
            // must survive ExitingPlayMode, even with domain reload disabled.
            s_ShuttingDown = false;
            ClearGlobals();
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                foreach (var pass in s_Instances) pass.ReleaseCameras();
        }
#endif
        private static void ClearCamera(ScriptableRenderContext context, Camera camera) => ClearGlobals();
        private static void ReleaseAll()
        {
            s_ShuttingDown = true;
            Shader.SetGlobalInt(CountId, 0);
            foreach (var pass in s_Instances) pass.ReleaseCameras();
            s_EmptyMap?.Dispose(); s_EmptyMap = null; CoreUtils.Destroy(s_White); s_White = null;
        }
        private void ReleaseCameras() { foreach (var state in _cameras.Values) state.Dispose(); _cameras.Clear(); }
        public void Dispose() { ReleaseCameras(); s_Instances.Remove(this); ClearGlobals(); }

        private sealed class Candidate
        {
            internal ulong Id;
            internal RayTracedShadowGpuLight Gpu;
            internal int Priority, Category, AdditionalIndex = -1, RectIndex = -1;
            internal bool Main;
            internal float Distance;
        }

        private sealed class CameraState : IDisposable
        {
            internal readonly TsukuyomiRayTracingScene Scene = new();
            internal readonly ResourceHub Hub;
            internal GraphicsBuffer Lights, AdditionalMap, RectMap;
            internal readonly Dictionary<ulong, (int slot, RayTracedShadowGpuLight gpu)> Previous = new();
            internal Matrix4x4 InverseViewProjection, Projection;
            internal Vector3 Position, Forward;
            internal Vector2 Jitter;
            internal bool Valid;
            internal int Frame, Ping, Samples, CasterMask;
            internal double LastTime;
            internal CameraState(ResourceHub hub) => Hub = hub;
            public void Dispose()
            {
                Scene.Dispose(); Lights?.Dispose(); AdditionalMap?.Dispose(); RectMap?.Dispose();
                Hub.ReleaseHistoryTexture(HistoryA); Hub.ReleaseHistoryTexture(HistoryB);
                Hub.ReleaseHistoryTexture(GeometryA); Hub.ReleaseHistoryTexture(GeometryB);
                Previous.Clear(); Valid = false;
            }
        }

        private void Gather(in FeatureGraphContext context, RectLightFrameData rectangles, out int additionalCount)
        {
            _candidates.Clear();
            var camera = context.CameraData.camera;
            var visible = context.LightData.visibleLights;
            additionalCount = Mathf.Min(context.LightData.additionalLightsCount,
                UniversalRenderPipeline.maxVisibleAdditionalLights);
            int additionalIndex = 0;
            for (int i = 0; i < visible.Length; i++)
            {
                bool main = i == context.LightData.mainLightIndex;
                int index = main ? -1 : additionalIndex++;
                if (!main && index >= additionalCount) continue;
                var light = visible[i].light;
                if (!light || !light.TryGetComponent<TsukuyomiRayTracedShadowLight>(out var setting) || !setting.IsUsable) continue;
                var t = light.transform;
                int type = light.type == LightType.Directional ? 3 : light.type == LightType.Spot ? 2 : 1;
                float outer = Mathf.Cos(0.5f * Mathf.Clamp(light.spotAngle, 0, 179) * Mathf.Deg2Rad);
                float inner = Mathf.Cos(0.5f * Mathf.Clamp(light.innerSpotAngle, 0, light.spotAngle) * Mathf.Deg2Rad);
                ulong id = EntityId.ToULong(light.GetEntityId());
                _candidates.Add(new Candidate {
                    Id = id, Main = main, AdditionalIndex = index, Priority = setting.ShadowPriority,
                    Category = main ? 0 : type == 3 ? 1 : 2, Distance = (t.position - camera.transform.position).sqrMagnitude,
                    Gpu = new RayTracedShadowGpuLight {
                        PositionRange = V(t.position, type == 3 ? _settings.DirectionalDistance : light.range),
                        DirectionType = V(t.forward, type), RightSize = V(t.right, 0), UpSize = V(t.up, 0),
                        ShapeParams = new Vector4(setting.SourceRadius, Mathf.Cos(0.5f * Mathf.Clamp(setting.AngularDiameter, 0, 180) * Mathf.Deg2Rad), outer, 1 / Mathf.Max(1e-5f, inner - outer)),
                        ShadowParams = new Vector4(light.shadowStrength, (uint)(id ^ (id >> 32)) & 0xffffff, 0, -1)
                    }
                });
            }
            for (int i = 0; i < rectangles.Lights.Length; i++)
            {
                var light = rectangles.Lights[i];
                if (light.ShadowMode != RayTracedShadowMode.RayTraced || !float.IsFinite(light.ShadowStrength) || light.ShadowStrength <= 0) continue;
                var t = light.transform; ulong id = EntityId.ToULong(light.GetEntityId());
                _candidates.Add(new Candidate {
                    Id = id, RectIndex = i, Priority = light.ShadowPriority, Category = 2,
                    Distance = (t.position - camera.transform.position).sqrMagnitude,
                    Gpu = new RayTracedShadowGpuLight {
                        PositionRange = V(t.position, light.Range), DirectionType = V(t.forward, 0),
                        RightSize = V(t.right, light.Width * 0.5f), UpSize = V(t.up, light.Height * 0.5f),
                        ShadowParams = new Vector4(Mathf.Clamp01(light.ShadowStrength), (uint)(id ^ (id >> 32)) & 0xffffff, 0, -1)
                    }
                });
            }
            _candidates.Sort((a, b) => {
                int order = b.Priority.CompareTo(a.Priority);
                if (order == 0) order = a.Category.CompareTo(b.Category);
                if (order == 0 && a.Category == 2) order = a.Distance.CompareTo(b.Distance);
                return order != 0 ? order : a.Id.CompareTo(b.Id);
            });
        }

        private static Vector4 V(Vector3 value, float w) => new(value.x, value.y, value.z, w);
        private static bool SameGeometry(in RayTracedShadowGpuLight a, in RayTracedShadowGpuLight b)
            => a.PositionRange == b.PositionRange && a.DirectionType == b.DirectionType && a.RightSize == b.RightSize
                && a.UpSize == b.UpSize && a.ShapeParams == b.ShapeParams;
        private static void EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            count = Mathf.Max(1, count);
            if (buffer != null && buffer.IsValid() && buffer.count == count) return;
            buffer?.Dispose(); buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
        }

        public override void RecordGraph(in FeatureGraphContext context)
        {
            ClearGlobals();
            RecordFallbackBindings(context);
            var camera = context.CameraData.camera;
            if (!_ready || !camera || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
                || context.CameraData.cameraTargetDescriptor.dimension != TextureDimension.Tex2D)
                return;
            var rectangles = _rectLights.PrepareFrame(context);
            Gather(context, rectangles, out int additionalCount);
            int count = Mathf.Min(_candidates.Count, _settings.MaxLights);
            if (count == 0)
            {
                if (_cameras.Remove(camera, out var old)) old.Dispose();
                return;
            }
            if (_candidates.Count > count && !_warnedOverflow)
            {
                Debug.LogWarning($"Tsukuyomi RT Shadows: {_candidates.Count} requested lights exceed the shared budget {count}. Other lights keep their existing shadow path.", camera);
                _warnedOverflow = true;
            }
            if (_candidates.Count <= count) _warnedOverflow = false;
            var depth = context.GetTexture(TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture));
            var normal = context.GetTexture(TextureSlot.Read("Normals", BuiltinTexture.CameraNormals));
            var motion = context.GetTexture(TextureSlot.Read("Motion", BuiltinTexture.MotionVectorColor));
            if (!depth.IsValid() || !normal.IsValid() || !motion.IsValid())
            {
                if (_cameras.TryGetValue(camera, out var unavailable)) unavailable.Valid = false;
                return;
            }
            if (!_cameras.TryGetValue(camera, out var state)) _cameras.Add(camera, state = new CameraState(context.ResourceHub));
            bool sceneChanged;
            using (SceneSampler.Auto()) sceneChanged = state.Scene.Update(camera, _settings.CasterMask);
            int width = context.CameraData.cameraTargetDescriptor.width, height = context.CameraData.cameraTargetDescriptor.height;
            var historyDesc = Descriptor(width, height, count, GraphicsFormat.R16G16B16A16_SFloat);
            var geometryDesc = Descriptor(width, height, 1, GraphicsFormat.R32G32B32A32_SFloat, false);
            var history0 = state.Hub.GetOrCreateHistoryTexture(HistoryA, historyDesc, out bool h0);
            var history1 = state.Hub.GetOrCreateHistoryTexture(HistoryB, historyDesc, out bool h1);
            var geometry0 = state.Hub.GetOrCreateHistoryTexture(GeometryA, geometryDesc, out bool g0);
            var geometry1 = state.Hub.GetOrCreateHistoryTexture(GeometryB, geometryDesc, out bool g1);
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(context.CameraData.GetProjectionMatrix(), true);
            Matrix4x4 inverse = (projection * context.CameraData.GetViewMatrix()).inverse;
            Vector2 jitter = CalculateJitter(projection, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
            bool historyValid = state.Valid && !sceneChanged && !h0 && !h1 && !g0 && !g1
                && state.Samples == _settings.Samples && state.CasterMask == _settings.CasterMask
                && Time.realtimeSinceStartupAsDouble - state.LastTime < 0.5
                && (state.Position - camera.transform.position).sqrMagnitude < 25
                && Vector3.Dot(state.Forward, camera.transform.forward) > 0.9f
                && state.Projection == camera.nonJitteredProjectionMatrix;
            var lights = new RayTracedShadowGpuLight[count];
            var additionalMap = new int[Mathf.Max(1, additionalCount)]; Array.Fill(additionalMap, -1);
            var rectMap = new int[Mathf.Max(1, rectangles.Lights.Length)]; Array.Fill(rectMap, -1);
            int mainIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var c = _candidates[i]; lights[i] = c.Gpu;
                float casterRange = c.Gpu.PositionRange.w + Mathf.Sqrt(c.Gpu.RightSize.w * c.Gpu.RightSize.w + c.Gpu.UpSize.w * c.Gpu.UpSize.w) + c.Gpu.ShapeParams.x;
                bool casterChanged = state.Scene.HasChangesForLight(c.Gpu.PositionRange, casterRange, c.Gpu.DirectionType.w == 3);
                if (historyValid && !casterChanged && state.Previous.TryGetValue(c.Id, out var previous) && SameGeometry(c.Gpu, previous.gpu))
                { lights[i].ShadowParams.z = 1; lights[i].ShadowParams.w = previous.slot; }
                if (c.Main) mainIndex = i;
                if (c.AdditionalIndex >= 0) additionalMap[c.AdditionalIndex] = i;
                if (c.RectIndex >= 0) { rectMap[c.RectIndex] = i; rectangles.ShadowSlots[c.Id] = i; }
            }
            state.Previous.Clear();
            for (int i = 0; i < count; i++) state.Previous[_candidates[i].Id] = (i, _candidates[i].Gpu);
            EnsureBuffer(ref state.Lights, count, RayTracedShadowGpuLight.Stride);
            EnsureBuffer(ref state.AdditionalMap, additionalMap.Length, sizeof(int));
            EnsureBuffer(ref state.RectMap, rectMap.Length, sizeof(int));
            var lightBuffer = context.RenderGraph.ImportBuffer(state.Lights);
            var addBuffer = context.RenderGraph.ImportBuffer(state.AdditionalMap);
            var rectBuffer = context.RenderGraph.ImportBuffer(state.RectMap);
            var raw = context.CreateTexture(ArrayDesc(width, height, count, GraphicsFormat.R16G16_SFloat, "RT Shadow Raw"));
            var previousHistory = context.RenderGraph.ImportTexture(state.Ping == 0 ? history0 : history1);
            var nextHistory = context.RenderGraph.ImportTexture(state.Ping == 0 ? history1 : history0);
            var previousGeometry = context.RenderGraph.ImportTexture(state.Ping == 0 ? geometry0 : geometry1);
            var nextGeometry = context.RenderGraph.ImportTexture(state.Ping == 0 ? geometry1 : geometry0);
            var parameters = new Parameters {
                Resolution = new Vector4(width, height, 1f / width, 1f / height), CameraPosition = V(camera.transform.position, 0),
                Inverse = inverse, PreviousInverse = state.Valid ? state.InverseViewProjection : inverse,
                JitterDelta = state.Valid ? state.Jitter - jitter : Vector2.zero,
                Count = count, Samples = _settings.Samples, Frame = state.Frame++, HistoryValid = historyValid ? 1 : 0
            };
            using (var node = context.AddUnsafe<TraceData>(Name, TraceSampler))
            {
                node.Builder.AllowPassCulling(false); node.Builder.AllowGlobalStateModification(true);
                node.Builder.UseBuffer(lightBuffer, AccessFlags.Write); node.Builder.UseBuffer(addBuffer, AccessFlags.Write);
                node.Builder.UseBuffer(rectBuffer, AccessFlags.Write);
                node.Builder.UseTexture(depth, AccessFlags.Read); node.Builder.UseTexture(normal, AccessFlags.Read);
                node.Builder.UseTexture(raw, AccessFlags.Write);
                var d = node.Data; d.Trace = _trace; d.State = state; d.Camera = camera; d.P = parameters;
                d.Lights = lights; d.AdditionalMap = additionalMap; d.RectMap = rectMap;
                d.Depth = depth; d.Normal = normal; d.Raw = raw;
                node.SetRenderFunc(static (d, ctx) => {
                    var cmd = ctx.cmd;
                    cmd.SetBufferData(d.State.Lights, d.Lights); cmd.SetBufferData(d.State.AdditionalMap, d.AdditionalMap); cmd.SetBufferData(d.State.RectMap, d.RectMap);
                    cmd.BeginSample(BuildSampler); cmd.BuildRayTracingAccelerationStructure(d.State.Scene.AccelerationStructure); cmd.EndSample(BuildSampler);
                    cmd.BeginSample(RaysSampler);
                    cmd.SetRayTracingShaderPass(d.Trace, "TsukuyomiRTShadow");
                    cmd.SetRayTracingAccelerationStructure(d.Trace, "_TsukuyomiRTShadowAccelerationStructure", d.State.Scene.AccelerationStructure);
                    cmd.SetRayTracingBufferParam(d.Trace, "_TsukuyomiRTShadowLights", d.State.Lights);
                    cmd.SetRayTracingTextureParam(d.Trace, "_RTShadowDepth", d.Depth); cmd.SetRayTracingTextureParam(d.Trace, "_RTShadowNormals", d.Normal);
                    cmd.SetRayTracingTextureParam(d.Trace, "_RTShadowRawRW", d.Raw);
                    cmd.SetRayTracingVectorParam(d.Trace, "_RTShadowResolution", d.P.Resolution);
                    cmd.SetRayTracingVectorParam(d.Trace, "_RTShadowCameraPosition", d.P.CameraPosition);
                    cmd.SetRayTracingMatrixParam(d.Trace, "_RTShadowInverseViewProjection", d.P.Inverse);
                    cmd.SetRayTracingIntParam(d.Trace, "_RTShadowFrameIndex", d.P.Frame);
                    cmd.SetRayTracingIntParam(d.Trace, "_RTShadowSampleCount", d.P.Samples);
                    cmd.SetRayTracingIntParam(d.Trace, "_RTShadowLightCount", d.P.Count);
                    cmd.SetRayTracingIntParam(d.Trace, "_RTShadowNormalsOctEncoded", Shader.IsKeywordEnabled("_GBUFFER_NORMALS_OCT") ? 1 : 0);
                    cmd.DispatchRays(d.Trace, "RayGenShadows", (uint)d.P.Resolution.x, (uint)d.P.Resolution.y, (uint)d.P.Count, d.Camera);
                    cmd.EndSample(RaysSampler);
                });
            }
            AddDenoise(context, "CopyGeometry", parameters, lightBuffer, depth, normal, motion, raw, previousHistory, previousGeometry, nextGeometry, nextGeometry, 0);
            AddDenoise(context, "Temporal", parameters, lightBuffer, depth, normal, motion, raw, previousHistory, previousGeometry, raw, nextHistory, 0);
            var filter0 = context.CreateTexture(ArrayDesc(width, height, count, GraphicsFormat.R16G16B16A16_SFloat, "RT Shadow Filter A"));
            var filter1 = context.CreateTexture(ArrayDesc(width, height, count, GraphicsFormat.R16G16B16A16_SFloat, "RT Shadow Filter B"));
            AddDenoise(context, "Atrous", parameters, lightBuffer, depth, normal, motion, raw, previousHistory, previousGeometry, nextHistory, filter0, 1);
            AddDenoise(context, "Atrous", parameters, lightBuffer, depth, normal, motion, raw, previousHistory, previousGeometry, filter0, filter1, 2);
            AddDenoise(context, "Atrous", parameters, lightBuffer, depth, normal, motion, raw, previousHistory, previousGeometry, filter1, filter0, 4);
            var output = context.CreateTexture(ArrayDesc(width, height, count, GraphicsFormat.R16_SFloat, "RT Shadow Visibility"));
            AddDenoise(context, "Finalize", parameters, lightBuffer, depth, normal, motion, raw, previousHistory, previousGeometry, filter0, output, 0);
            using (var node = context.AddUnsafe<PublishData>("Publish RT Shadows"))
            {
                node.Builder.AllowGlobalStateModification(true); node.Builder.AllowPassCulling(false);
                node.Builder.UseTexture(output, AccessFlags.Read); node.Builder.UseBuffer(addBuffer, AccessFlags.Read); node.Builder.UseBuffer(rectBuffer, AccessFlags.Read);
                node.Builder.SetGlobalTextureAfterPass(output, OutputId);
                var d = node.Data; d.Output = output; d.Additional = addBuffer; d.Rect = rectBuffer;
                d.Count = count; d.Main = mainIndex; d.AdditionalCount = additionalCount; d.RectCount = rectangles.Lights.Length;
                // Fixed capacity also works when a later camera selects more lights: Unity fixes
                // a global array's capacity on its first upload. Used only for baked-shadow mixing.
                d.Strengths = new float[16];
                for (int i = 0; i < count; i++) d.Strengths[i] = lights[i].ShadowParams.x;
                d.FallbackFade = CalculateFallbackFade(camera);
                node.SetRenderFunc(static (d, ctx) => {
                    ctx.cmd.SetGlobalBuffer(AdditionalMapId, d.Additional); ctx.cmd.SetGlobalBuffer(RectMapId, d.Rect);
                    ctx.cmd.SetGlobalFloatArray(StrengthsId, d.Strengths);
                    ctx.cmd.SetGlobalVector(FallbackFadeId, d.FallbackFade);
                    ctx.cmd.SetGlobalInt(MainId, d.Main); ctx.cmd.SetGlobalInt(AdditionalCountId, d.AdditionalCount);
                    ctx.cmd.SetGlobalInt(RectCountId, d.RectCount); ctx.cmd.SetGlobalInt(CountId, d.Count);
                });
            }
            state.Ping ^= 1; state.InverseViewProjection = inverse; state.Projection = camera.nonJitteredProjectionMatrix;
            state.Position = camera.transform.position; state.Forward = camera.transform.forward; state.Valid = true;
            state.Jitter = jitter;
            state.Samples = _settings.Samples; state.CasterMask = _settings.CasterMask; state.LastTime = Time.realtimeSinceStartupAsDouble;
        }

        private static void RecordFallbackBindings(in FeatureGraphContext context)
        {
            if (s_EmptyMap == null || !s_EmptyMap.IsValid() || !s_White) return;
            // RT-off and zero-light variants still statically reference mapping SRVs.
            // CPU Shader globals alone do not replace the preceding camera's GPU binding.
            using var node = context.AddUnsafe<FallbackData>("Reset RT Shadow Bindings");
            node.Builder.AllowGlobalStateModification(true);
            node.Builder.AllowPassCulling(false);
            node.Data.Map = context.RenderGraph.ImportBuffer(s_EmptyMap);
            node.Data.White = new RenderTargetIdentifier(s_White);
            node.Builder.UseBuffer(node.Data.Map, AccessFlags.Read);
            node.SetRenderFunc(static (d, ctx) => {
                ctx.cmd.SetGlobalInt(CountId, 0); ctx.cmd.SetGlobalInt(MainId, -1);
                ctx.cmd.SetGlobalInt(AdditionalCountId, 0); ctx.cmd.SetGlobalInt(RectCountId, 0);
                ctx.cmd.SetGlobalBuffer(AdditionalMapId, d.Map); ctx.cmd.SetGlobalBuffer(RectMapId, d.Map);
                ctx.cmd.SetGlobalTexture(OutputId, d.White);
            });
        }

        private static Vector4 CalculateFallbackFade(Camera camera)
        {
            // Shadow atlas passes need not run for an RT-only light. Publish independent
            // parameters so Distance ShadowMask still fades into baked shadows without
            // reading stale URP globals from another camera. Match URP's squared-distance curve.
            var asset = UniversalRenderPipeline.asset;
            float distance = asset ? Mathf.Min(asset.shadowDistance, camera.farClipPlane) : 0;
            if (distance <= 0 || distance < camera.nearClipPlane) return Vector4.zero;
            float farSquared = distance * distance;
            float border = Mathf.Clamp01(asset.cascadeBorder);
            if (border < .0001f) return new Vector4(1000, -farSquared * 1000, 0, 0);
            float nearSquared = (1 - border) * (1 - border) * farSquared;
            float scale = 1 / Mathf.Max(farSquared - nearSquared, 1e-6f);
            return new Vector4(scale, -nearSquared * scale, 0, 0);
        }

        private static Vector2 CalculateJitter(Matrix4x4 projection, Matrix4x4 original)
        {
            var p = projection * new Vector4(0, 0, -1, 1);
            var q = original * new Vector4(0, 0, -1, 1);
            if (Mathf.Abs(p.w) < 1e-6f || Mathf.Abs(q.w) < 1e-6f) return Vector2.zero;
            return new Vector2((p.x / p.w - q.x / q.w) * 0.5f,
                (p.y / p.w - q.y / q.w) * (SystemInfo.graphicsUVStartsAtTop ? -0.5f : 0.5f));
        }

        private static RenderTextureDescriptor Descriptor(int width, int height, int count, GraphicsFormat format, bool array = true)
            => new(width, height) { graphicsFormat = format, depthStencilFormat = GraphicsFormat.None, msaaSamples = 1,
                dimension = array ? TextureDimension.Tex2DArray : TextureDimension.Tex2D, volumeDepth = count,
                enableRandomWrite = true, sRGB = false, useMipMap = false };
        private static TextureDesc ArrayDesc(int width, int height, int count, GraphicsFormat format, string name)
            => new(width, height) { name = name, colorFormat = format, dimension = TextureDimension.Tex2DArray,
                slices = count, enableRandomWrite = true, clearBuffer = false, filterMode = FilterMode.Point };
        private struct Parameters
        {
            internal Vector4 Resolution, CameraPosition;
            internal Vector2 JitterDelta;
            internal Matrix4x4 Inverse, PreviousInverse;
            internal int Count, Samples, Frame, HistoryValid;
        }
        private sealed class TraceData
        {
            internal RayTracingShader Trace; internal CameraState State; internal Camera Camera; internal Parameters P;
            internal RayTracedShadowGpuLight[] Lights; internal int[] AdditionalMap, RectMap;
            internal TextureHandle Depth, Normal, Raw;
        }
        private sealed class PublishData
        {
            internal TextureHandle Output; internal BufferHandle Additional, Rect;
            internal int Count, Main, AdditionalCount, RectCount;
            internal float[] Strengths;
            internal Vector4 FallbackFade;
        }
        private sealed class FallbackData
        {
            internal BufferHandle Map;
            internal RenderTargetIdentifier White;
        }
        private sealed class DenoiseData
        {
            internal ComputeShader Shader; internal int Kernel, Step; internal string Operation; internal Parameters P;
            internal BufferHandle Lights;
            internal TextureHandle Depth, Normal, Motion, Raw, History, Geometry, Input, Output;
        }
        private void AddDenoise(in FeatureGraphContext context, string operation, Parameters p, BufferHandle lights,
            TextureHandle depth, TextureHandle normal, TextureHandle motion, TextureHandle raw,
            TextureHandle history, TextureHandle geometry, TextureHandle input, TextureHandle output, int step)
        {
            using var node = context.AddCompute<DenoiseData>("RT Shadow " + operation, DenoiseSampler);
            var d = node.Data; d.Shader = _denoise; d.Kernel = _denoise.FindKernel(operation); d.Operation = operation; d.Step = step; d.P = p;
            d.Lights = lights; d.Depth = depth; d.Normal = normal; d.Motion = motion; d.Raw = raw;
            d.History = history; d.Geometry = geometry; d.Input = input; d.Output = output;
            node.Builder.UseBuffer(lights, AccessFlags.Read);
            node.Builder.UseTexture(output, AccessFlags.Write);
            node.Builder.UseTexture(depth, AccessFlags.Read); node.Builder.UseTexture(normal, AccessFlags.Read);
            if (operation == "Temporal")
            { node.Builder.UseTexture(motion, AccessFlags.Read); node.Builder.UseTexture(raw, AccessFlags.Read); node.Builder.UseTexture(history, AccessFlags.Read); node.Builder.UseTexture(geometry, AccessFlags.Read); }
            if (operation == "Atrous" || operation == "Finalize") node.Builder.UseTexture(input, AccessFlags.Read);
            node.Builder.AllowPassCulling(false);
            node.SetRenderFunc(static (d, ctx) => {
                var cmd = ctx.cmd; var s = d.Shader; int k = d.Kernel;
                cmd.SetComputeVectorParam(s, "_RTShadowResolution", d.P.Resolution); cmd.SetComputeVectorParam(s, "_RTShadowCameraPosition", d.P.CameraPosition);
                cmd.SetComputeMatrixParam(s, "_RTShadowInverseViewProjection", d.P.Inverse); cmd.SetComputeMatrixParam(s, "_RTShadowPreviousInverseViewProjection", d.P.PreviousInverse);
                cmd.SetComputeVectorParam(s, "_RTShadowJitterDelta", new Vector4(d.P.JitterDelta.x, d.P.JitterDelta.y, 0, 0));
                cmd.SetComputeIntParam(s, "_RTShadowLightCount", d.P.Count); cmd.SetComputeIntParam(s, "_RTShadowHistoryValid", d.P.HistoryValid);
                cmd.SetComputeIntParam(s, "_RTShadowNormalsOctEncoded", Shader.IsKeywordEnabled("_GBUFFER_NORMALS_OCT") ? 1 : 0);
                cmd.SetComputeIntParam(s, "_RTShadowFilterStep", d.Step); cmd.SetComputeBufferParam(s, k, "_TsukuyomiRTShadowLights", d.Lights);
                cmd.SetComputeTextureParam(s, k, "_RTShadowDepth", d.Depth); cmd.SetComputeTextureParam(s, k, "_RTShadowNormals", d.Normal);
                if (d.Operation == "CopyGeometry") cmd.SetComputeTextureParam(s, k, "_RTShadowGeometryRW", d.Output);
                else if (d.Operation == "Temporal")
                {
                    cmd.SetComputeTextureParam(s, k, "_RTShadowMotion", d.Motion); cmd.SetComputeTextureParam(s, k, "_RTShadowRaw", d.Raw);
                    cmd.SetComputeTextureParam(s, k, "_RTShadowHistory", d.History); cmd.SetComputeTextureParam(s, k, "_RTShadowPreviousGeometry", d.Geometry);
                    cmd.SetComputeTextureParam(s, k, "_RTShadowTemporalRW", d.Output);
                }
                else
                {
                    cmd.SetComputeTextureParam(s, k, "_RTShadowFilterInput", d.Input);
                    cmd.SetComputeTextureParam(s, k, d.Operation == "Finalize" ? "_RTShadowOutputRW" : "_RTShadowFilterRW", d.Output);
                }
                cmd.DispatchCompute(s, k, Mathf.CeilToInt(d.P.Resolution.x / 8), Mathf.CeilToInt(d.P.Resolution.y / 8), d.Operation == "CopyGeometry" ? 1 : d.P.Count);
            });
        }
    }
}
