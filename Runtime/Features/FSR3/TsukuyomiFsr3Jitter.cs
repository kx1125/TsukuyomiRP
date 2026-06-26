using System.Reflection;
using Tsukuyomi.Rendering.FSR3;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal static class TsukuyomiFsr3Jitter
    {
        private static readonly MethodInfo SetViewProjectionAndJitterMatrixMethod = typeof(CameraData).GetMethod("SetViewProjectionAndJitterMatrix", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo GetProjectionMatrixNoJitterMethod = typeof(CameraData).GetMethod("GetProjectionMatrixNoJitter", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool TryApply(ref RenderingData renderingData, TsukuyomiFsr3Settings settings, Camera camera, out Vector2 jitterOffset)
        {
            jitterOffset = Vector2.zero;

            if (settings == null || !settings.Enabled || camera == null)
                return false;

            RenderTextureDescriptor cameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            int renderWidth = Mathf.Max(1, cameraDescriptor.width);
            int renderHeight = Mathf.Max(1, cameraDescriptor.height);
            int targetWidth = renderingData.cameraData.targetTexture != null ? renderingData.cameraData.targetTexture.width : camera.pixelWidth;
            int displayWidth = Mathf.Max(renderWidth, targetWidth);

            int jitterPhaseCount = Fsr3Upscaler.GetJitterPhaseCount(renderWidth, displayWidth);
            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, Time.frameCount, jitterPhaseCount);
            jitterOffset = new Vector2(jitterX, jitterY);

            CameraData cameraData = renderingData.cameraData;
            Matrix4x4 projectionMatrix = GetProjectionMatrixNoJitter(cameraData, cameraData.GetProjectionMatrix());
            Matrix4x4 jitterMatrix = CalculateJitterMatrix(jitterOffset, renderWidth, renderHeight);
            SetViewProjectionAndJitterMatrix(cameraData, cameraData.GetViewMatrix(), projectionMatrix, jitterMatrix);
            return true;
        }

        private static Matrix4x4 CalculateJitterMatrix(Vector2 jitterOffset, int renderWidth, int renderHeight)
        {
            float offsetX = jitterOffset.x * (2.0f / renderWidth);
            float offsetY = jitterOffset.y * (2.0f / renderHeight);
            return Matrix4x4.Translate(new Vector3(offsetX, offsetY, 0.0f));
        }

        private static Matrix4x4 GetProjectionMatrixNoJitter(CameraData cameraData, Matrix4x4 fallbackProjectionMatrix)
        {
            if (GetProjectionMatrixNoJitterMethod == null)
                return fallbackProjectionMatrix;

            object boxedCameraData = cameraData;
            return (Matrix4x4)GetProjectionMatrixNoJitterMethod.Invoke(boxedCameraData, new object[] { 0 });
        }

        private static void SetViewProjectionAndJitterMatrix(CameraData cameraData, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Matrix4x4 jitterMatrix)
        {
            if (SetViewProjectionAndJitterMatrixMethod == null)
                return;

            object boxedCameraData = cameraData;
            SetViewProjectionAndJitterMatrixMethod.Invoke(boxedCameraData, new object[] { viewMatrix, projectionMatrix, jitterMatrix });
        }
    }
}
