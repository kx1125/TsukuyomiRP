using NUnit.Framework;
using UnityEngine;

namespace Tsukuyomi.Rendering.Tests
{
    public sealed class SceneScanCurveReferenceTests
    {
        [TestCase(0f, 90f)]
        [TestCase(0.2f / 0.6f, 120f)]
        [TestCase(0.4f / 0.6f, 90f)]
        public void HorizontalFovMatchesUnrealKeysInRealSeconds(float seconds, float expected)
        {
            var root = new GameObject("UE horizontal FOV reference");
            try
            {
                var controller = root.AddComponent<SceneScanController>();
                controller.SetEnabled(true);
                controller.Advance(seconds);
                Assert.That(controller.ReferenceHorizontalFov, Is.EqualTo(expected).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void InvalidDeltaTimeDoesNotChangeScanOrRandomState()
        {
            var root = new GameObject("Scan invalid time input");
            try
            {
                var controller = root.AddComponent<SceneScanController>();
                controller.SetEnabled(true);
                controller.Advance(0.75f);
                float progress = controller.Progress;
                float random = controller.Random01;
                controller.Advance(-1f);
                controller.Advance(float.NaN);
                controller.Advance(float.PositiveInfinity);
                Assert.That(controller.Progress, Is.EqualTo(progress));
                Assert.That(controller.Random01, Is.EqualTo(random));
                Assert.That(controller.TargetEnabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // Values read from the live UE 5.2 assets, not from the Unity implementation.
        [TestCase(0.1f, 0.8404584f, 0.187999f, 0f, 0f, 0f, 105.325226f)]
        [TestCase(0.2f, 1f, 0.399907f, 0f, 0.652243f, 0f, 120f)]
        [TestCase(0.3f, 1f, 0.611391f, 0.110962f, 0.650781f, 0f, 104.893471f)]
        [TestCase(0.5f, 1f, 0.935768f, 0.493448f, 0f, 0f, 90f)]
        [TestCase(0.75f, 1f, 1f, 0.845806f, 0f, 0.181987f, 90f)]
        [TestCase(0.9f, 1f, 1f, 0.966110f, 0f, 0.613039f, 90f)]
        public void MatchesLiveUnrealCurveSamples(float time, float blend, float screen,
            float world, float flash, float reveal, float horizontalFov)
        {
            var root = new GameObject("UE scan curve reference");
            try
            {
                var controller = root.AddComponent<SceneScanController>();
                controller.SetEnabled(true);
                controller.Advance(time / controller.FadeInSpeed);
                Assert.That(controller.Blend, Is.EqualTo(blend).Within(0.00002f));
                Assert.That(controller.ScreenProgress, Is.EqualTo(screen).Within(0.00002f));
                Assert.That(controller.WorldProgress, Is.EqualTo(world).Within(0.00002f));
                Assert.That(controller.Flash, Is.EqualTo(flash).Within(0.00002f));
                Assert.That(controller.Reveal, Is.EqualTo(reveal).Within(0.00002f));
                Assert.That(controller.ReferenceHorizontalFov, Is.EqualTo(horizontalFov).Within(0.0002f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RandomStateIsIndependentOfFramePartitionAndReleasedWhenDisabled()
        {
            var rootA = new GameObject("Coarse scan simulation");
            var rootB = new GameObject("Fine scan simulation");
            try
            {
                var a = rootA.AddComponent<SceneScanController>();
                var b = rootB.AddComponent<SceneScanController>();
                a.SetEnabled(true);
                b.SetEnabled(true);
                bool sawGlitch = false;
                for (int quarter = 0; quarter < 80; quarter++)
                {
                    a.Advance(0.25f);
                    for (int frame = 0; frame < 16; frame++)
                        b.Advance(0.015625f);
                    Assert.That(a.Random01, Is.EqualTo(b.Random01));
                    Assert.That(a.HorizontalGlitch, Is.EqualTo(b.HorizontalGlitch));
                    Assert.That(a.ChromaticSplit, Is.EqualTo(b.ChromaticSplit));
                    sawGlitch |= a.HorizontalGlitch != 0f;
                }
                Assert.That(sawGlitch, Is.True, "The comparison must include an actual timed glitch, not only zero values.");
                a.enabled = false;
                Assert.That(a.HorizontalGlitch, Is.Zero);
                Assert.That(a.ChromaticSplit, Is.Zero);
                Assert.That(a.Random01, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootA);
                Object.DestroyImmediate(rootB);
            }
        }
    }
}
