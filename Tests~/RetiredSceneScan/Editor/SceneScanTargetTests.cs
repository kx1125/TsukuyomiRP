using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tsukuyomi.Rendering.Tests
{
    public sealed class SceneScanTargetTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly List<GameObject> _roots = new();

        // Registry and renderer ownership are internal production details. Observe them
        // through reflection rather than granting the test assembly additional visibility.
        private static IReadOnlyList<ScanTarget> Registry =>
            (IReadOnlyList<ScanTarget>)typeof(ScanTarget).GetProperty("Registry", StaticMembers).GetValue(null);

        private static IReadOnlyList<Renderer> Discovered(ScanTarget target) =>
            (IReadOnlyList<Renderer>)typeof(ScanTarget).GetProperty("CachedRenderers", InstanceMembers).GetValue(target);

        private static bool Owns(ScanTarget target, Renderer renderer) =>
            (bool)typeof(ScanTarget).GetMethod("OwnsRenderer", InstanceMembers).Invoke(target, new object[] { renderer });

        private GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            _roots.Add(root);
            return root;
        }

        private static GameObject CreateChild(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _roots.Count - 1; i >= 0; i--)
            {
                if (_roots[i])
                    Object.DestroyImmediate(_roots[i]);
            }
            _roots.Clear();
        }

        [Test]
        public void ReenableRegistersExactlyOnceAndDestroyUnregisters()
        {
            GameObject root = CreateRoot("Scan target registration test");
            ScanTarget target = root.AddComponent<ScanTarget>();
            Assert.That(Occurrences(target), Is.EqualTo(1));

            target.enabled = false;
            Assert.That(Occurrences(target), Is.Zero);
            target.enabled = true;
            target.RefreshRenderers();
            Assert.That(Occurrences(target), Is.EqualTo(1));

            Object.DestroyImmediate(target);
            Assert.That(Occurrences(target), Is.Zero);
        }

        [Test]
        public void InactiveObjectLeavesRegistryAndReturnsWhenEnabled()
        {
            GameObject root = CreateRoot("Scan target activation test");
            ScanTarget target = root.AddComponent<ScanTarget>();
            root.SetActive(false);
            Assert.That(Occurrences(target), Is.Zero);
            root.SetActive(true);
            Assert.That(Occurrences(target), Is.EqualTo(1));
            target.enabled = false;
            root.SetActive(false);
            root.SetActive(true);
            Assert.That(Occurrences(target), Is.Zero,
                "Reactivating an object must not register a disabled ScanTarget.");
        }

        [Test]
        public void NestedTargetOwnsRendererAndDisabledChildFallsBackToParent()
        {
            GameObject root = CreateRoot("Parent scan target");
            GameObject child = CreateChild(root, "Nested scan target");
            Renderer renderer = CreateChild(child, "Nested mesh").AddComponent<MeshRenderer>();
            ScanTarget parentTarget = root.AddComponent<ScanTarget>();
            parentTarget.Category = ScanTargetCategory.Character;
            ScanTarget childTarget = child.AddComponent<ScanTarget>();
            childTarget.Category = ScanTargetCategory.Enemy;

            Assert.That(Discovered(parentTarget), Does.Contain(renderer));
            Assert.That(Discovered(childTarget), Does.Contain(renderer));
            Assert.That(Owns(parentTarget, renderer), Is.False);
            Assert.That(Owns(childTarget, renderer), Is.True);

            childTarget.enabled = false;
            Assert.That(Occurrences(childTarget), Is.Zero);
            Assert.That(Owns(parentTarget, renderer), Is.True,
                "Disabling a nested marker must return its meshes to the active parent marker.");
            childTarget.enabled = true;
            Assert.That(Owns(parentTarget, renderer), Is.False);
            Assert.That(Owns(childTarget, renderer), Is.True);
        }

        [Test]
        public void DiscoveryIncludesSkinnedMeshesAndIgnoresOtherRendererKinds()
        {
            GameObject root = CreateRoot("Scan target discovery test");
            Renderer mesh = root.AddComponent<MeshRenderer>();
            Renderer skinned = CreateChild(root, "Skinned child").AddComponent<SkinnedMeshRenderer>();
            Renderer trail = CreateChild(root, "Unsupported trail").AddComponent<TrailRenderer>();
            ScanTarget target = root.AddComponent<ScanTarget>();

            Assert.That(Discovered(target), Has.Count.EqualTo(2));
            Assert.That(Discovered(target), Does.Contain(mesh));
            Assert.That(Discovered(target), Does.Contain(skinned));
            CollectionAssert.DoesNotContain(Discovered(target), trail);

            target.IncludeChildren = false;
            target.RefreshRenderers();
            Assert.That(Discovered(target), Has.Count.EqualTo(1));
            Assert.That(Discovered(target), Does.Contain(mesh));
        }

        [Test]
        public void ExplicitListOverridesDiscoveryDeduplicatesAndDropsDestroyedRenderers()
        {
            GameObject root = CreateRoot("Explicit scan target test");
            Renderer local = root.AddComponent<MeshRenderer>();
            Renderer remote = CreateRoot("Explicit external skin").AddComponent<SkinnedMeshRenderer>();
            ScanTarget target = root.AddComponent<ScanTarget>();
            target.Renderers = new[] { remote, remote, null };
            target.RefreshRenderers();

            Assert.That(Discovered(target), Has.Count.EqualTo(1));
            Assert.That(Discovered(target), Does.Contain(remote));
            CollectionAssert.DoesNotContain(Discovered(target), local);
            Assert.That(Owns(target, remote), Is.True);

            Object.DestroyImmediate(remote);
            target.RefreshRenderers();
            Assert.That(Discovered(target), Is.Empty);
        }

        private static int Occurrences(ScanTarget target)
        {
            int count = 0;
            IReadOnlyList<ScanTarget> registry = Registry;
            for (int i = 0; i < registry.Count; i++)
            {
                if (ReferenceEquals(registry[i], target))
                    count++;
            }
            return count;
        }
    }
}
