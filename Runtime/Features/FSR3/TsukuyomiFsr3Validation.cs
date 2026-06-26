using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal static class TsukuyomiFsr3Validation
    {
        public static bool TryValidateForEnqueue(
            ref RenderingData renderingData,
            HashSet<ulong> loggedTaaConflictCameras,
            HashSet<ulong> loggedCameraStackCameras,
            out Camera camera,
            out ulong cameraId)
        {
            camera = renderingData.cameraData.camera;
            cameraId = camera != null ? EntityId.ToULong(camera.GetEntityId()) : 0UL;

            if (camera == null)
                return false;

            if (!IsSupportedCameraType(renderingData.cameraData.cameraType) || renderingData.cameraData.isPreviewCamera || renderingData.cameraData.xr.enabled)
                return false;

            if (renderingData.cameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing)
            {
                if (loggedTaaConflictCameras != null && loggedTaaConflictCameras.Add(cameraId))
                {
                    Debug.LogError($"Tsukuyomi FSR3 is enabled on camera '{camera.name}', but Unity Temporal Anti-Aliasing is also enabled. Disable TAA in the camera's Universal Additional Camera Data before using Tsukuyomi FSR3.", camera);
                }

                return false;
            }

            loggedTaaConflictCameras?.Remove(cameraId);

            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return false;

            if (camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                if (additionalCameraData.renderType == CameraRenderType.Overlay)
                    return false;

                if (additionalCameraData.cameraStack != null && additionalCameraData.cameraStack.Count > 0)
                {
                    if (loggedCameraStackCameras != null && loggedCameraStackCameras.Add(cameraId))
                    {
                        Debug.LogWarning($"Tsukuyomi FSR3 is skipped on camera '{camera.name}' because camera stacks are not supported by the current FSR3 integration.", camera);
                    }

                    return false;
                }
            }

            return true;
        }

        public static bool IsSupportedCamera(UniversalCameraData cameraData)
        {
            if (cameraData == null || cameraData.camera == null)
                return false;

            if (!IsSupportedCameraType(cameraData.cameraType) || cameraData.isPreviewCamera || cameraData.xr.enabled)
                return false;

            if (cameraData.renderType == CameraRenderType.Overlay)
                return false;

            if (cameraData.camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                if (additionalCameraData.renderType == CameraRenderType.Overlay)
                    return false;

                if (additionalCameraData.cameraStack != null && additionalCameraData.cameraStack.Count > 0)
                    return false;
            }

            return true;
        }

        private static bool IsSupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Game || cameraType == CameraType.SceneView;
        }
    }
}

