using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    public sealed class RenderObjectContext
    {
        public Camera Camera => CameraData.camera;
        public UniversalCameraData CameraData { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float Aspect => (float)Width / Height;

        internal bool HasView { get; private set; }
        internal Matrix4x4 ViewMatrix { get; private set; }
        internal Matrix4x4 ProjectionMatrix { get; private set; }
        internal Vector3 CameraPosition { get; private set; }
        internal Rect Viewport { get; private set; }

        internal RenderObjectContext() { }

        internal void Reset(UniversalCameraData cameraData, int width, int height)
        {
            CameraData = cameraData;
            Width = width;
            Height = height;
            HasView = false;
        }

        public void SetViewProjectionMatrices(
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            Vector3 cameraPosition)
        {
            SetViewProjectionMatrices(
                viewMatrix,
                projectionMatrix,
                cameraPosition,
                new Rect(0.0f, 0.0f, Width, Height));
        }

        public void SetViewProjectionMatrices(
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            Vector3 cameraPosition,
            Rect viewport)
        {
            ViewMatrix = viewMatrix;
            ProjectionMatrix = projectionMatrix;
            CameraPosition = cameraPosition;
            Viewport = viewport;
            HasView = true;
        }
    }
}
