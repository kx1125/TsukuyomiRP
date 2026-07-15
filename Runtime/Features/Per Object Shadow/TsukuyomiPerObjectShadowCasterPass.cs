using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiPerObjectShadowCasterPass : ScriptableRenderPass, IDisposable
    {
        public const int MaxShadowCount = 16;

        private static readonly int ShadowMapId = Shader.PropertyToID("_PerObjSceneShadowMap");
        private static readonly int ShadowCountId = Shader.PropertyToID("_PerObjSceneShadowCount");
        private static readonly int ShadowMatricesId = Shader.PropertyToID("_PerObjSceneShadowMatrices");
        private static readonly int ShadowMapRectsId = Shader.PropertyToID("_PerObjSceneShadowMapRects");
        private static readonly int ShadowCasterIdsId = Shader.PropertyToID("_PerObjSceneShadowCasterIds");
        private static readonly int ShadowOffset0Id = Shader.PropertyToID("_PerObjSceneShadowOffset0");
        private static readonly int ShadowOffset1Id = Shader.PropertyToID("_PerObjSceneShadowOffset1");
        private static readonly int ShadowMapSizeId = Shader.PropertyToID("_PerObjSceneShadowMapSize");
        private static readonly int ShadowBiasesId = Shader.PropertyToID("_PerObjShadowBiases");
        private static readonly int PerObjPcssParams0Id = Shader.PropertyToID("_PerObjShadowPcssParams0");
        private static readonly int PerObjPcssParams1Id = Shader.PropertyToID("_PerObjShadowPcssParams1");
        private static readonly int PerObjPcssProjsId = Shader.PropertyToID("_PerObjShadowPcssProjs");
        private static readonly int ShadowBiasId = Shader.PropertyToID("_ShadowBias");
        private static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int LightPositionId = Shader.PropertyToID("_LightPosition");

        private readonly Matrix4x4[] _shadowMatrixArray = new Matrix4x4[MaxShadowCount];
        private readonly Vector4[] _shadowMapRectArray = new Vector4[MaxShadowCount];
        private readonly float[] _shadowCasterIdArray = new float[MaxShadowCount];
        private readonly Vector4[] _shadowBiasArray = new Vector4[MaxShadowCount];
        private readonly Vector4[] _perObjPcssParams0 = new Vector4[MaxShadowCount];
        private readonly Vector4[] _perObjPcssParams1 = new Vector4[MaxShadowCount];
        private readonly Vector4[] _perObjPcssProjs = new Vector4[MaxShadowCount];
        private readonly TsukuyomiShadowCasterManager _casterManager = new();

        private bool _enabled;
        private bool _pcssEnabled;
        private TsukuyomiPerObjectShadowResolvedSettings _settings;
        private TsukuyomiPcssResolvedSettings _pcssSettings;
        private RenderTextureDescriptor _shadowMapDescriptor;
        private bool _hasShadowMapDescriptor;
        private int _tileResolution;
        private int _shadowMapSizeInTile;

        public TsukuyomiPerObjectShadowCasterPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 2;
            profilingSampler = new ProfilingSampler("Tsukuyomi Per Object Shadow");
        }

        public bool Configure(
            TsukuyomiPipelineProfile profile,
            TsukuyomiPerObjectShadowVolume volume,
            TsukuyomiPcssVolume pcssVolume)
        {
            _enabled = false;
            _pcssEnabled = false;

            if (profile == null)
                return false;

            _settings = TsukuyomiPerObjectShadowResolvedSettings.From(profile, volume);
            _pcssSettings = TsukuyomiPcssResolvedSettings.From(profile, pcssVolume);
            _pcssEnabled = _pcssSettings.Enabled;

            return _settings.Enabled;
        }

        public void Dispose()
        {
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalShadowData shadowData = frameData.Get<UniversalShadowData>();

            _enabled = _settings.Enabled && !cameraData.isPreviewCamera;
            if (_enabled)
            {
                _casterManager.Cull(cameraData, lightData, MaxShadowCount, _settings.ShadowLengthOffset);
                SetupShadowMap();
            }

            if (!_enabled || _casterManager.VisibleCount <= 0 || !_hasShadowMapDescriptor)
            {
                using var builder = renderGraph.AddRasterRenderPass("Tsukuyomi Per Object Shadow (Clear)", out PassData passData, profilingSampler);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData _, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalInt(ShadowCountId, 0);
                });
                return;
            }

            TextureHandle shadowTexture;
            using (var builder = renderGraph.AddRasterRenderPass("Tsukuyomi Per Object Shadowmap", out PassData passData, profilingSampler))
            {
                InitPassData(passData, lightData, shadowData);
                passData.ShadowmapTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, _shadowMapDescriptor, "_PerObjSceneShadowMap", true);
                builder.SetRenderAttachmentDepth(passData.ShadowmapTexture);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    RenderShadowMap(context.cmd, data);
                });
                shadowTexture = passData.ShadowmapTexture;
            }

            using (var builder = renderGraph.AddRasterRenderPass("Tsukuyomi Set Per Object Shadow Globals", out PassData passData, profilingSampler))
            {
                InitPassData(passData, lightData, shadowData);
                passData.ShadowmapTexture = shadowTexture;
                builder.UseTexture(shadowTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    SetupShadowGlobalVariables(context.cmd, data);
                });
            }

            if (cameraData.renderer is UniversalRenderer renderer)
            {
                TextureHandle cameraTarget = resourceData.activeColorTexture.IsValid()
                    ? resourceData.activeColorTexture
                    : resourceData.activeDepthTexture;
                renderer.SetupRenderGraphCameraProperties(renderGraph, cameraTarget);
            }
        }

        private void SetupShadowMap()
        {
            if (!_enabled || _casterManager.VisibleCount <= 0)
                return;

            _tileResolution = Mathf.Max(1, (int)_settings.TileResolution);
            _shadowMapSizeInTile = Mathf.CeilToInt(Mathf.Sqrt(_casterManager.VisibleCount));
            int shadowRTSize = _shadowMapSizeInTile * _tileResolution;
            _shadowMapDescriptor = CreateShadowMapDescriptor(shadowRTSize, shadowRTSize, _settings.DepthBits);
            _hasShadowMapDescriptor = true;
        }

        private static RenderTextureDescriptor CreateShadowMapDescriptor(int width, int height, TsukuyomiPerObjectShadowDepthBits depthBits)
        {
            GraphicsFormat depthFormat = GetDepthStencilFormat(depthBits);
            RenderTextureDescriptor descriptor = new(width, height, GraphicsFormat.None, depthFormat)
            {
                msaaSamples = 1,
                shadowSamplingMode = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap)
                    ? ShadowSamplingMode.CompareDepths
                    : ShadowSamplingMode.None
            };

            return descriptor;
        }

        private static GraphicsFormat GetDepthStencilFormat(TsukuyomiPerObjectShadowDepthBits depthBits)
        {
            return depthBits switch
            {
                TsukuyomiPerObjectShadowDepthBits.Depth32 => GetSupportedDepthFormat(
                    GraphicsFormat.D32_SFloat,
                    GraphicsFormat.D24_UNorm_S8_UInt,
                    GraphicsFormat.D16_UNorm),
                TsukuyomiPerObjectShadowDepthBits.Depth24 => GetSupportedDepthFormat(
                    GraphicsFormat.D24_UNorm_S8_UInt,
                    GraphicsFormat.D32_SFloat,
                    GraphicsFormat.D16_UNorm),
                _ => GetSupportedDepthFormat(
                    GraphicsFormat.D16_UNorm,
                    GraphicsFormat.D24_UNorm_S8_UInt,
                    GraphicsFormat.D32_SFloat)
            };
        }

        private static GraphicsFormat GetSupportedDepthFormat(params GraphicsFormat[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (SystemInfo.IsFormatSupported(candidates[i], GraphicsFormatUsage.Render))
                    return candidates[i];
            }

            return GraphicsFormatUtility.GetDepthStencilFormat(16, 0);
        }


        private void InitPassData(PassData passData, UniversalLightData lightData, UniversalShadowData shadowData)
        {
            passData.Pass = this;
            passData.LightData = lightData;
            passData.ShadowData = shadowData;
            passData.CasterManager = _casterManager;
            passData.ShadowMapSizeInTile = _shadowMapSizeInTile;
            passData.TileResolution = _tileResolution;
            passData.ShadowMatrixArray = _shadowMatrixArray;
            passData.ShadowMapRectArray = _shadowMapRectArray;
            passData.ShadowCasterIdArray = _shadowCasterIdArray;
            passData.ShadowBiasArray = _shadowBiasArray;
            passData.PerObjPcssParams0 = _perObjPcssParams0;
            passData.PerObjPcssParams1 = _perObjPcssParams1;
            passData.PerObjPcssProjs = _perObjPcssProjs;
            passData.PcssSettings = _pcssSettings;
            passData.PcssEnabled = _pcssEnabled;
        }

        private static void RenderShadowMap(RasterCommandBuffer cmd, PassData data)
        {
            cmd.SetGlobalDepthBias(1.0f, 2.5f);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, false);

            int mainLightIndex = data.LightData.mainLightIndex;
            VisibleLight mainLight = data.LightData.visibleLights[mainLightIndex];

            for (int i = 0; i < data.CasterManager.VisibleCount; i++)
            {
                data.CasterManager.GetMatrices(i, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix);

                Vector4 shadowBias = ShadowUtils.GetShadowBias(ref mainLight, mainLightIndex, data.ShadowData, projectionMatrix, data.Pass._shadowMapDescriptor.width);
                data.ShadowBiasArray[i] = shadowBias;
                SetupShadowCasterConstants(cmd, ref mainLight, shadowBias);

                Vector2Int tilePos = new(i % data.ShadowMapSizeInTile, i / data.ShadowMapSizeInTile);
                DrawShadow(cmd, data, i, tilePos, viewMatrix, projectionMatrix);
                data.ShadowMatrixArray[i] = data.Pass.GetShadowMatrix(tilePos, viewMatrix, projectionMatrix);
                data.ShadowMapRectArray[i] = data.Pass.GetShadowMapRect(tilePos);
                data.ShadowCasterIdArray[i] = data.CasterManager.GetId(i);
            }

            cmd.SetGlobalDepthBias(0.0f, 0.0f);
        }

        private static void SetupShadowCasterConstants(RasterCommandBuffer cmd, ref VisibleLight shadowLight, Vector4 shadowBias)
        {
            Vector3 lightDirection = -shadowLight.localToWorldMatrix.GetColumn(2);
            Vector3 lightPosition = shadowLight.localToWorldMatrix.GetColumn(3);
            cmd.SetGlobalVector(ShadowBiasId, shadowBias);
            cmd.SetGlobalVector(LightDirectionId, new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
            cmd.SetGlobalVector(LightPositionId, new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
        }

        private static void DrawShadow(RasterCommandBuffer cmd, PassData data, int casterIndex, Vector2Int tilePos, Matrix4x4 view, Matrix4x4 projection)
        {
            cmd.SetViewProjectionMatrices(view, projection);
            Rect viewport = new(tilePos * data.TileResolution, new Vector2(data.TileResolution, data.TileResolution));
            cmd.SetViewport(viewport);
            cmd.EnableScissorRect(new Rect(viewport.x + 4.0f, viewport.y + 4.0f, viewport.width - 8.0f, viewport.height - 8.0f));
            data.CasterManager.Draw(cmd, casterIndex);
            cmd.DisableScissorRect();
        }

        private static void SetupShadowGlobalVariables(RasterCommandBuffer cmd, PassData data)
        {
            int width = data.Pass._shadowMapDescriptor.width;
            int height = data.Pass._shadowMapDescriptor.height;
            float invWidth = 1.0f / width;
            float invHeight = 1.0f / height;

            cmd.SetGlobalTexture(ShadowMapId, data.ShadowmapTexture);
            cmd.SetGlobalInt(ShadowCountId, data.CasterManager.VisibleCount);
            cmd.SetGlobalMatrixArray(ShadowMatricesId, data.ShadowMatrixArray);
            cmd.SetGlobalVectorArray(ShadowMapRectsId, data.ShadowMapRectArray);
            cmd.SetGlobalFloatArray(ShadowCasterIdsId, data.ShadowCasterIdArray);
            cmd.SetGlobalVectorArray(ShadowBiasesId, data.ShadowBiasArray);
            cmd.SetGlobalVector(ShadowOffset0Id, new Vector4(-0.5f * invWidth, -0.5f * invHeight, 0.5f * invWidth, -0.5f * invHeight));
            cmd.SetGlobalVector(ShadowOffset1Id, new Vector4(-0.5f * invWidth, 0.5f * invHeight, 0.5f * invWidth, 0.5f * invHeight));
            cmd.SetGlobalVector(ShadowMapSizeId, new Vector4(invWidth, invHeight, width, height));

            if (data.PcssEnabled)
                SetupPerObjectPcssGlobals(cmd, data);
        }

        private static void SetupPerObjectPcssGlobals(RasterCommandBuffer cmd, PassData data)
        {
            TsukuyomiPcssResolvedSettings settings = data.PcssSettings;
            float lightAngularDiameter = Mathf.Max(0.001f, settings.AngularDiameter);
            float dirLightDepthToRadius = Mathf.Tan(0.5f * Mathf.Deg2Rad * lightAngularDiameter);
            float minFilterAngularDiameter = Mathf.Max(settings.BlockerSearchAngularDiameter, settings.MinFilterMaxAngularDiameter);
            float halfMinFilterTangent = Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(minFilterAngularDiameter, lightAngularDiameter));
            float halfBlockerSearchTangent = Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(settings.BlockerSearchAngularDiameter, lightAngularDiameter));

            for (int i = 0; i < data.CasterManager.VisibleCount; i++)
            {
                data.CasterManager.GetMatrices(i, out _, out Matrix4x4 projectionMatrix);
                float shadowmapDepthToRadialScale = Mathf.Abs(projectionMatrix.m00 / projectionMatrix.m22);
                float depthToRadius = Mathf.Max(0.000001f, dirLightDepthToRadius * shadowmapDepthToRadialScale);

                data.PerObjPcssParams0[i] = new Vector4(
                    depthToRadius,
                    1.0f / depthToRadius,
                    settings.MaxPenumbraSize / Mathf.Max(0.000001f, 2.0f * halfMinFilterTangent),
                    settings.MaxSamplingDistance);

                data.PerObjPcssParams1[i] = new Vector4(
                    Mathf.Max(0.0f, settings.MinFilterSizeTexels),
                    1.0f / Mathf.Max(0.000001f, halfMinFilterTangent * shadowmapDepthToRadialScale),
                    1.0f / Mathf.Max(0.000001f, halfBlockerSearchTangent * shadowmapDepthToRadialScale),
                    0.0f);

                data.PerObjPcssProjs[i] = new Vector4(projectionMatrix.m00, projectionMatrix.m11, projectionMatrix.m22, projectionMatrix.m23);
            }

            cmd.SetGlobalVectorArray(PerObjPcssParams0Id, data.PerObjPcssParams0);
            cmd.SetGlobalVectorArray(PerObjPcssParams1Id, data.PerObjPcssParams1);
            cmd.SetGlobalVectorArray(PerObjPcssProjsId, data.PerObjPcssProjs);
        }

        private Matrix4x4 GetShadowMatrix(Vector2Int tilePos, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                projectionMatrix.m20 = -projectionMatrix.m20;
                projectionMatrix.m21 = -projectionMatrix.m21;
                projectionMatrix.m22 = -projectionMatrix.m22;
                projectionMatrix.m23 = -projectionMatrix.m23;
            }

            float oneOverTileCount = 1.0f / _shadowMapSizeInTile;
            Matrix4x4 textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f * oneOverTileCount;
            textureScaleAndBias.m11 = 0.5f * oneOverTileCount;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = (0.5f + tilePos.x) * oneOverTileCount;
            textureScaleAndBias.m13 = (0.5f + tilePos.y) * oneOverTileCount;
            textureScaleAndBias.m23 = 0.5f;
            return textureScaleAndBias * projectionMatrix * viewMatrix;
        }

        private Vector4 GetShadowMapRect(Vector2Int tilePos)
        {
            return new Vector4(tilePos.x, tilePos.x + 1, tilePos.y, tilePos.y + 1) / _shadowMapSizeInTile;
        }

        private sealed class PassData
        {
            public TsukuyomiPerObjectShadowCasterPass Pass;
            public TextureHandle ShadowmapTexture;
            public UniversalLightData LightData;
            public UniversalShadowData ShadowData;
            public TsukuyomiShadowCasterManager CasterManager;
            public int ShadowMapSizeInTile;
            public int TileResolution;
            public Matrix4x4[] ShadowMatrixArray;
            public Vector4[] ShadowMapRectArray;
            public float[] ShadowCasterIdArray;
            public Vector4[] ShadowBiasArray;
            public Vector4[] PerObjPcssParams0;
            public Vector4[] PerObjPcssParams1;
            public Vector4[] PerObjPcssProjs;
            public TsukuyomiPcssResolvedSettings PcssSettings;
            public bool PcssEnabled;
        }

    }
}







