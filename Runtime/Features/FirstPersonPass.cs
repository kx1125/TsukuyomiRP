using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "FirstPersonPlugin")]
    public sealed class FirstPersonPass : RenderPassDefinition
    {
        [UnityEngine.Range(1, 179)] public float FieldOfView = 60;
        [UnityEngine.Min(.001f)] public float NearClipPlane = .01f;
        [UnityEngine.Min(.002f)] public float FarClipPlane = 1000;
        public string CameraTag = "MainCamera";
        public UnityEngine.LayerMask CullingMask = 1 << 6;
        public bool RenderOpaque = true;
        public bool RenderTransparent = true;
        public bool UseOcclusionCulling = true;
        public override bool AllowMultiple => true;
        public FirstPersonPass() => InjectionPoint = InjectionPoint.BeforePostProcess;
        public override string Name => "First Person View";
        public override IRenderPassRuntime CreateRuntime()
            => new FirstPersonViewPass
            { FieldOfView = FieldOfView, NearClipPlane = NearClipPlane, FarClipPlane = FarClipPlane,
                CameraTag = CameraTag, CullingMask = CullingMask, RenderOpaque = RenderOpaque,
                RenderTransparent = RenderTransparent, UseOcclusionCulling = UseOcclusionCulling,
                InjectionPoint = InjectionPoint };
    }

    [System.Serializable]
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    [MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "TsukuyomiFirstPersonViewPass")]
    internal sealed class FirstPersonViewPass : RenderObjectPass, IRenderMaskViewProvider
    {
        [Range(1.0f, 179.0f)]
        public float FieldOfView = 60.0f;

        [Min(0.001f)]
        public float NearClipPlane = 0.01f;

        [Min(0.002f)]
        public float FarClipPlane = 1000.0f;

        public string CameraTag = "MainCamera";

        public override string Name => "FirstPersonViewPass";

        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data) => ValidateSettings();

        public override void ValidateSettings()
        {
            base.ValidateSettings();
            FieldOfView = Mathf.Clamp(FieldOfView, 1.0f, 179.0f);
            NearClipPlane = Mathf.Max(0.001f, NearClipPlane);
            FarClipPlane = Mathf.Max(NearClipPlane + 0.001f, FarClipPlane);
            CameraTag ??= string.Empty;
        }

        protected override bool IsCameraSupported(in FrameContext frame)
        {
            Camera camera = frame.CameraData.camera;
            return camera
                && camera.cameraType == CameraType.Game
                && frame.CameraData.renderType == CameraRenderType.Base
                && !camera.stereoEnabled
                && (string.IsNullOrEmpty(CameraTag) || camera.CompareTag(CameraTag));
        }

        protected override bool Render(RenderObjectContext ctx)
        {
            Camera camera = ctx.Camera;
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = Matrix4x4.Perspective(
                FieldOfView,
                ctx.Aspect,
                NearClipPlane,
                FarClipPlane);
            ApplyCameraJitter(camera, ref projectionMatrix);
            ctx.SetViewProjectionMatrices(
                viewMatrix,
                projectionMatrix,
                camera.transform.position);
            return true;
        }

        public bool TryGetMaskView(UniversalCameraData cameraData, out RenderMaskView view)
        {
            view = default;
            var camera = cameraData.camera;
            if (!Enabled || !RenderTransparent || CullingMask.value == 0 || !camera || camera.cameraType != CameraType.Game
                || cameraData.renderType != CameraRenderType.Base || camera.stereoEnabled
                || (!string.IsNullOrEmpty(CameraTag) && !camera.CompareTag(CameraTag))) return false;
            var desc = cameraData.cameraTargetDescriptor;
            var projection = Matrix4x4.Perspective(Mathf.Clamp(FieldOfView, 1f, 179f),
                (float)Mathf.Max(1, desc.width) / Mathf.Max(1, desc.height), Mathf.Max(.001f, NearClipPlane),
                Mathf.Max(NearClipPlane + .001f, FarClipPlane));
            ApplyCameraJitter(camera, ref projection);
            view = new RenderMaskView { View = camera.worldToCameraMatrix, Projection = projection,
                LayerMask = CullingMask.value, UseOcclusionCulling = UseOcclusionCulling };
            return true;
        }

        private static void ApplyCameraJitter(Camera camera, ref Matrix4x4 projectionMatrix)
        {
            Matrix4x4 jitteredProjection = camera.projectionMatrix;
            Matrix4x4 nonJitteredProjection = camera.nonJitteredProjectionMatrix;
            projectionMatrix.m02 += jitteredProjection.m02 - nonJitteredProjection.m02;
            projectionMatrix.m12 += jitteredProjection.m12 - nonJitteredProjection.m12;
        }

    }
}
