using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering.Tests
{
    public sealed class SceneScanControllerTests
    {
        private GameObject _root;
        private SceneScanController _controller;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Scene scan controller test");
            _camera = _root.AddComponent<Camera>();
            _camera.enabled = false;
            _controller = _root.AddComponent<SceneScanController>();
            _controller.TargetCamera = _camera;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void UEReferenceCurvesKeepBlendWorldAndFlashIndependent()
        {
            _controller.SetEnabled(true);
            _controller.Advance(0.2f / 0.6f);
            Assert.That(_controller.Progress, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(_controller.Blend, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(_controller.WorldProgress, Is.Zero.Within(0.0001f));
            _controller.Advance((0.246997f - 0.2f) / 0.6f);
            Assert.That(_controller.Flash, Is.EqualTo(1f).Within(0.0001f));
            _controller.Advance((0.5f - 0.246997f) / 0.6f);
            Assert.That(_controller.WorldProgress, Is.EqualTo(0.49345f).Within(0.0001f));
            Assert.That(_controller.Flash, Is.Zero.Within(0.0001f));
            _controller.Advance(10f);
            Assert.That(_controller.Progress, Is.EqualTo(1f));
            Assert.That(_controller.WorldProgress, Is.EqualTo(1f));
            Assert.That(_controller.ScreenProgress, Is.EqualTo(1f));
        }

        [Test]
        public void MidTransitionReversalContinuesAndFullyRestores()
        {
            _controller.SetEnabled(true);
            _controller.Advance(0.5f);
            Assert.That(_controller.Progress, Is.EqualTo(0.3f).Within(0.0001f));
            _controller.Toggle();
            _controller.Advance(0.1f);
            Assert.That(_controller.Progress, Is.EqualTo(0.18f).Within(0.0001f));
            _controller.Toggle();
            _controller.Advance(0.1f);
            Assert.That(_controller.Progress, Is.EqualTo(0.24f).Within(0.0001f));
            _controller.SetEnabled(false);
            _controller.Advance(1f);
            Assert.That(_controller.Progress, Is.Zero);
            Assert.That(SceneScanController.TryGet(_camera, out _), Is.False);
        }

        [Test]
        public void ScanOnlyAffectsTheAssignedBaseCameraAndDisableRemovesIt()
        {
            _controller.SetEnabled(true);
            _controller.Advance(1f);
            Assert.That(SceneScanController.TryGet(_camera, out var found), Is.True);
            Assert.That(found, Is.SameAs(_controller));
            var other = new GameObject("Unassigned camera");
            try
            {
                Assert.That(SceneScanController.TryGet(other.AddComponent<Camera>(), out _), Is.False);
                var data = _camera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Overlay;
                Assert.That(SceneScanController.TryGet(_camera, out _), Is.False);
                data.renderType = CameraRenderType.Base;
                _controller.enabled = false;
                Assert.That(SceneScanController.TryGet(_camera, out _), Is.False);
                Assert.That(_controller.Progress, Is.Zero);
                Assert.That(_controller.TargetEnabled, Is.False);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
