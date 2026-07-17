using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiPlanarReflectionPass : RasterPass
    {
        private const string TextureName = "_TsukuyomiPlanarReflectionTexture";
        private const string LegacyCorrectTextureName = "_PlanarReflectionTexture";
        private const string LegacyMisspelledTextureName = "_PlanarRefectionTexture";

        private static readonly int TextureId = Shader.PropertyToID(TextureName);
        private static readonly int LegacyCorrectTextureId = Shader.PropertyToID(LegacyCorrectTextureName);
        private static readonly int LegacyMisspelledTextureId = Shader.PropertyToID(LegacyMisspelledTextureName);
        private static readonly int TexelSizeId = Shader.PropertyToID("_TsukuyomiPlanarReflectionTexelSize");
        private static readonly int EnabledId = Shader.PropertyToID("_TsukuyomiPlanarReflectionEnabled");
        private static readonly int WorldSpaceCameraPosId = Shader.PropertyToID("_WorldSpaceCameraPos");

        private readonly List<ShaderTagId> _shaderTagIds = new();
        private TsukuyomiPlanarReflectionPlane _plane;
        private float _renderTextureScale = 0.5f;
        private int _layerMask = -1;
        private bool _configured;

        public override string Name => "Tsukuyomi Planar Reflection";
        public override int Priority => 1000;

        public bool Configure(TsukuyomiPipelineProfile profile)
        {
            _configured = false;
            _plane = null;

            if (profile == null || !profile.EnablePlanarReflection)
                return false;

            if (!TsukuyomiPlanarReflectionPlane.TryGetActivePlane(out _plane))
                return false;

            _renderTextureScale = Mathf.Max(0.01f, profile.PlanarReflectionRenderTextureScale);
            _layerMask = profile.PlanarReflectionLayerMask.value;

            EnsureShaderTags();

            _configured = true;
            return true;
        }

        public override bool IsActive(in FrameContext frame)
        {
            UniversalCameraData cameraData = frame.CameraData;
            return base.IsActive(frame)
                && _configured
                && _plane
                && cameraData != null
                && cameraData.camera
                && !cameraData.isPreviewCamera
                && cameraData.cameraType != CameraType.Reflection
                && cameraData.renderType == CameraRenderType.Base;
        }

        public static void ClearGlobals()
        {
            Shader.SetGlobalInt(EnabledId, 0);
        }

        public override void Record(in RasterPassContext context)
        {
            if (!_configured || !_plane)
                return;

            Camera camera = context.CameraData.camera;
            if (!camera || !camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
            {
                RecordClear(context);
                return;
            }

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            int width = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.width * _renderTextureScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.height * _renderTextureScale));

            if (!_plane.TryGetPlane(out Vector3 planePosition, out Vector3 planeNormal))
            {
                RecordClear(context);
                return;
            }

            Vector3 cameraPosition = context.CameraData.worldSpaceCameraPos;
            if (Vector3.Dot(planeNormal, cameraPosition - planePosition) < 0.0f)
                planeNormal = -planeNormal;

            Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(planeNormal, planePosition);
            Matrix4x4 reflectionView = camera.worldToCameraMatrix * reflectionMatrix;
            Matrix4x4 skyboxProjection = camera.projectionMatrix;
            Vector4 clipPlane = CameraSpacePlane(reflectionView, planePosition, planeNormal, _plane.clipPlaneOffset, 1.0f);
            Matrix4x4 reflectionProjection = CalculateObliqueMatrix(skyboxProjection, clipPlane);
            Vector3 reflectedCameraPosition = reflectionMatrix.MultiplyPoint(context.CameraData.worldSpaceCameraPos);

            cullingParameters.cullingMatrix = reflectionProjection * reflectionView;
            cullingParameters.origin = reflectedCameraPosition;
            cullingParameters.cullingMask = unchecked((uint)_layerMask);

            CullContextData cullContext = context.FrameData.Get<CullContextData>();
            CullingResults reflectionCullResults = cullContext.Cull(ref cullingParameters);

            UniversalRenderingData renderingData = context.FrameData.Get<UniversalRenderingData>();
            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                _shaderTagIds,
                renderingData,
                context.CameraData,
                context.LightData,
                SortingCriteria.CommonOpaque);

            SortingSettings sortingSettings = drawingSettings.sortingSettings;
            sortingSettings.criteria = SortingCriteria.CommonOpaque;
            sortingSettings.worldToCameraMatrix = reflectionView;
            sortingSettings.cameraPosition = reflectedCameraPosition;
            drawingSettings.sortingSettings = sortingSettings;

            FilteringSettings filteringSettings = new(RenderQueueRange.opaque, _layerMask);
            RendererListHandle rendererList = context.RenderGraph.CreateRendererList(
                new RendererListParams(reflectionCullResults, drawingSettings, filteringSettings));
            RendererListHandle skyboxRendererList = context.RenderGraph.CreateSkyboxRendererList(
                camera,
                skyboxProjection,
                reflectionView);

            TextureHandle reflectionColor = context.RenderGraph.CreateTexture(CreateColorDesc(cameraDescriptor, width, height));
            TextureHandle reflectionDepth = context.RenderGraph.CreateTexture(CreateDepthDesc(width, height));

            context.Builder.UseAllGlobalTextures(true);
            context.Builder.SetRenderAttachment(reflectionColor, 0, AccessFlags.Write);
            context.Builder.SetRenderAttachmentDepth(reflectionDepth, AccessFlags.Write);
            context.Builder.UseRendererList(skyboxRendererList);
            context.Builder.UseRendererList(rendererList);
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.AllowPassCulling(false);
            context.Builder.SetGlobalTextureAfterPass(reflectionColor, TextureId);
            context.Builder.SetGlobalTextureAfterPass(reflectionColor, LegacyCorrectTextureId);
            context.Builder.SetGlobalTextureAfterPass(reflectionColor, LegacyMisspelledTextureId);

            UniversalResourceData resourceData = context.FrameData.Get<UniversalResourceData>();
            if (resourceData.mainShadowsTexture.IsValid())
                context.Builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
            if (resourceData.additionalShadowsTexture.IsValid())
                context.Builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);

            Matrix4x4 cameraView = context.CameraData.GetViewMatrix();
            Matrix4x4 cameraProjection = context.CameraData.GetProjectionMatrix();
            Rect reflectionViewport = new(0.0f, 0.0f, width, height);
            Rect cameraViewport = new(0.0f, 0.0f, cameraDescriptor.width, cameraDescriptor.height);
            Vector4 texelSize = new(1.0f / width, 1.0f / height, width, height);

            context.SetRenderFunc((data, graphContext) =>
            {
                graphContext.cmd.SetViewport(reflectionViewport);
                graphContext.cmd.ClearRenderTarget(true, true, Color.clear);
                graphContext.cmd.SetViewProjectionMatrices(reflectionView, skyboxProjection);
                graphContext.cmd.SetGlobalVector(WorldSpaceCameraPosId, reflectedCameraPosition);
                graphContext.cmd.SetInvertCulling(true);
                graphContext.cmd.DrawRendererList(skyboxRendererList);
                graphContext.cmd.SetViewProjectionMatrices(reflectionView, reflectionProjection);
                graphContext.cmd.DrawRendererList(rendererList);
                graphContext.cmd.SetInvertCulling(false);
                graphContext.cmd.SetGlobalVector(WorldSpaceCameraPosId, cameraPosition);
                graphContext.cmd.SetViewProjectionMatrices(cameraView, cameraProjection);
                graphContext.cmd.SetViewport(cameraViewport);

                graphContext.cmd.SetGlobalVector(TexelSizeId, texelSize);
                graphContext.cmd.SetGlobalInt(EnabledId, 1);
            });
        }

        private static void RecordClear(in RasterPassContext context)
        {
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.AllowPassCulling(false);
            context.SetRenderFunc((data, graphContext) =>
            {
                graphContext.cmd.SetGlobalInt(EnabledId, 0);
            });
        }

        private void EnsureShaderTags()
        {
            if (_shaderTagIds.Count != 0)
                return;

            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
        }

        private static TextureDesc CreateColorDesc(RenderTextureDescriptor cameraDescriptor, int width, int height)
        {
            GraphicsFormat format = cameraDescriptor.graphicsFormat;
            if (format == GraphicsFormat.None || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
            {
                format = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.R8G8B8A8_UNorm;
            }

            RenderTextureDescriptor descriptor = new(width, height, format, GraphicsFormat.None)
            {
                msaaSamples = 1,
                mipCount = 0,
                useMipMap = true,
                autoGenerateMips = true
            };

            return new TextureDesc(descriptor)
            {
                name = TextureName,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                clearColor = Color.clear
            };
        }

        private static TextureDesc CreateDepthDesc(int width, int height)
        {
            RenderTextureDescriptor descriptor = new(width, height, GraphicsFormat.None, GetDepthFormat())
            {
                msaaSamples = 1
            };

            return new TextureDesc(descriptor)
            {
                name = "_TsukuyomiPlanarReflectionDepth",
                filterMode = FilterMode.Point,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                clearColor = Color.clear
            };
        }

        private static GraphicsFormat GetDepthFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, GraphicsFormatUsage.Render))
                return GraphicsFormat.D24_UNorm_S8_UInt;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat, GraphicsFormatUsage.Render))
                return GraphicsFormat.D32_SFloat;
            return GraphicsFormat.D16_UNorm;
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector3 normal, Vector3 position)
        {
            Vector4 plane = new(normal.x, normal.y, normal.z, -Vector3.Dot(normal, position));
            Matrix4x4 reflection = Matrix4x4.identity;
            reflection.m00 = 1.0f - 2.0f * plane.x * plane.x;
            reflection.m01 = -2.0f * plane.x * plane.y;
            reflection.m02 = -2.0f * plane.x * plane.z;
            reflection.m03 = -2.0f * plane.w * plane.x;
            reflection.m10 = -2.0f * plane.y * plane.x;
            reflection.m11 = 1.0f - 2.0f * plane.y * plane.y;
            reflection.m12 = -2.0f * plane.y * plane.z;
            reflection.m13 = -2.0f * plane.w * plane.y;
            reflection.m20 = -2.0f * plane.z * plane.x;
            reflection.m21 = -2.0f * plane.z * plane.y;
            reflection.m22 = 1.0f - 2.0f * plane.z * plane.z;
            reflection.m23 = -2.0f * plane.w * plane.z;
            return reflection;
        }

        private static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 position, Vector3 normal, float offset, float sideSign)
        {
            Vector3 offsetPosition = position + normal * offset;
            Vector3 cameraPosition = worldToCamera.MultiplyPoint(offsetPosition);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 plane)
        {
            Vector4 clipCorner = new(Mathf.Sign(plane.x), Mathf.Sign(plane.y), 1.0f, 1.0f);
            Vector4 viewCorner = projection.inverse * clipCorner;
            Vector4 scaledPlane = plane * (2.0f / Vector4.Dot(plane, viewCorner));
            Matrix4x4 oblique = projection;
            oblique.SetRow(2, scaledPlane - projection.GetRow(3));
            return oblique;
        }

    }
}
