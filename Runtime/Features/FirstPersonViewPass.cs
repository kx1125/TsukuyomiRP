using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    [MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "TsukuyomiFirstPersonViewPass")]
    public sealed class FirstPersonViewPass : RenderObjectPass
    {
        [Range(1.0f, 179.0f)]
        public float FieldOfView = 60.0f;

        [Min(0.001f)]
        public float NearClipPlane = 0.01f;

        [Min(0.002f)]
        public float FarClipPlane = 1000.0f;

        public string CameraTag = "MainCamera";

        public override string Name => "FirstPersonViewPass";

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

        private static void ApplyCameraJitter(Camera camera, ref Matrix4x4 projectionMatrix)
        {
            Matrix4x4 jitteredProjection = camera.projectionMatrix;
            Matrix4x4 nonJitteredProjection = camera.nonJitteredProjectionMatrix;
            projectionMatrix.m02 += jitteredProjection.m02 - nonJitteredProjection.m02;
            projectionMatrix.m12 += jitteredProjection.m12 - nonJitteredProjection.m12;
        }

    }
}
