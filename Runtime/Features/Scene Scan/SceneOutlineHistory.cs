using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    internal sealed class SceneOutlineHistory : BufferedTextureHistory
    {
        private int _coverageId, _depthId;
        private Hash128 _coverageKey, _depthKey;
        private SceneOutlineTemporal _owner;
        private bool _scanNormals;
        private int _frame = -1, _signature;
        private Matrix4x4 _projection, _view;
        private Vector2 _jitter;
        private Vector3 _position;
        private Quaternion _rotation;

        internal RTHandle Coverage => GetCurrentFrameRT(_coverageId);
        internal RTHandle PreviousCoverage => GetPreviousFrameRT(_coverageId);
        internal RTHandle Depth => GetCurrentFrameRT(_depthId);
        internal RTHandle PreviousDepth => GetPreviousFrameRT(_depthId);

        public override void OnCreate(BufferedRTHandleSystem storage, uint typeId)
        {
            base.OnCreate(storage, typeId);
            _coverageId = MakeId(0);
            _depthId = MakeId(1);
        }

        internal bool Begin(SceneOutlineTemporal owner, Camera camera, in RenderTextureDescriptor target,
            int signature, Matrix4x4 projection, Matrix4x4 view, Vector2 jitter, bool scanNormals,
            out Matrix4x4 previousView, out Vector2 jitterDelta, out bool resetScan)
        {
            var coverage = TextureDescriptors.HistoryColor(target, target.width, target.height, GraphicsFormat.R16G16B16A16_SFloat);
            var depth = TextureDescriptors.HistoryColor(target, target.width, target.height, GraphicsFormat.R32_SFloat);
            bool resized = EnsureAllocated(_coverageId, ref _coverageKey, ref coverage, FilterMode.Bilinear, "SceneOutlineCoverage")
                | EnsureAllocated(_depthId, ref _depthKey, ref depth, FilterMode.Point, "SceneOutlineEyeDepth");
            var transform = camera.transform;
            bool valid = !resized && ReferenceEquals(_owner, owner) && _frame == Time.frameCount - 1
                && _signature == signature && _projection == projection
                && (transform.position - _position).sqrMagnitude <= 25f
                && Quaternion.Angle(transform.rotation, _rotation) <= 45f;
            previousView = valid ? _view : view;
            jitterDelta = valid ? _jitter - jitter : Vector2.zero;
            resetScan = !valid || _scanNormals != scanNormals;
            _scanNormals = scanNormals;
            _owner = owner;
            _frame = Time.frameCount;
            _signature = signature;
            _projection = projection;
            _view = view;
            _jitter = jitter;
            _position = transform.position;
            _rotation = transform.rotation;
            return valid;
        }

        internal void Release(SceneOutlineTemporal owner)
        {
            if (ReferenceEquals(owner, _owner)) Reset();
        }

        public override void Reset()
        {
            ReleaseHistoryFrameRT(_coverageId);
            ReleaseHistoryFrameRT(_depthId);
            _coverageKey = _depthKey = default;
            _owner = null;
            _frame = -1;
        }
    }
}
