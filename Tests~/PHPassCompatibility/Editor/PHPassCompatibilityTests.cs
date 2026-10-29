using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering.Tests
{
    public class PHPassCompatibilityTests
    {
        private const string LegacyAsset = "Assets/PHLegacyProfileTest.asset";
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void LegacyYamlMigratesParametersAndSurvivesSaveReload()
        {
            File.WriteAllText(LegacyAsset, @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_Script: {fileID: 11500000, guid: af9c99167c04c5d42a83befff9521b0e, type: 3}
  m_Name: PH Legacy Profile Test
  EnableRectLights: 1
  MaxRectLights: 23
  EnableRayTracedShadows: 1
  RayTracedShadowSamples: 19
  EnableMSVO: 1
  MsvoIntensity: 1.73
  MsvoCompositionMode: 1
  EnableGTAO: 1
  EnableSssSkin: 1
  SssSkinHighPrecision: 0
  SssSkinScatteringRadius: 2.4
  EnableCustomBloom: 1
  EnableTsukuyomiPostProcessing: 0
  Passes: []
");
            try
            {
                AssetDatabase.ImportAsset(LegacyAsset, ImportAssetOptions.ForceSynchronousImport);
                var profile = AssetDatabase.LoadAssetAtPath<TsukuyomiPipelineProfile>(LegacyAsset);
                Assert.That(profile, Is.Not.Null);
                Assert.That(profile.WasLegacyMigrated, Is.True);
                Assert.That(profile.SchemaVersion, Is.EqualTo(TsukuyomiPipelineProfile.CurrentSchemaVersion));
                Assert.That(profile.Passes.OfType<MsvoPass>().Single().MsvoIntensity, Is.EqualTo(1.73f));
                Assert.That(profile.Passes.OfType<MsvoPass>().Single().MsvoCompositionMode, Is.EqualTo(TsukuyomiMsvoCompositionMode.Lighting));
                Assert.That(profile.Passes.OfType<RectLightPass>().Single().MaxRectLights, Is.EqualTo(23));
                Assert.That(profile.Passes.OfType<RayTracedShadowPass>().Single().RayTracedShadowSamples, Is.EqualTo(19));
                Assert.That(profile.Passes.OfType<SssSkinPass>().Single().SssSkinHighPrecision, Is.False);
                Assert.That(profile.Passes.OfType<SssSkinPass>().Single().SssSkinScatteringRadius, Is.EqualTo(2.4f));
                Assert.That(profile.Passes.OfType<BloomPass>().Single().Enabled, Is.False);
                Assert.That(profile.Passes.OfType<MsvoPass>().Single().Enabled, Is.True);
                var ids = profile.Passes.Select(p => p.NodeId).ToArray();
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
                using (var session = new PassSession(profile, false))
                    Assert.That(session.Valid, Is.True, string.Join("\n", session.Errors));
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                Resources.UnloadAsset(profile);
                AssetDatabase.ImportAsset(LegacyAsset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                profile = AssetDatabase.LoadAssetAtPath<TsukuyomiPipelineProfile>(LegacyAsset);
                Assert.That(profile.Passes.Select(p => p.NodeId), Is.EqualTo(ids));
                Assert.That(File.ReadAllText(LegacyAsset), Does.Contain("_schemaVersion: 1"));
            }
            finally { AssetDatabase.DeleteAsset(LegacyAsset); }
        }

        [Test]
        public void FreshProfilesStayEmptyAndExistingNodesAreNotDuplicated()
        {
            var profile = ScriptableObject.CreateInstance<TsukuyomiPipelineProfile>();
            try
            {
                Assert.That(profile.Passes, Is.Empty);
                var node = new MsvoPass { MsvoIntensity = 2.3f };
                profile.Passes.Add(node);
                typeof(TsukuyomiPipelineProfile).GetField("_schemaVersion", AllInstance).SetValue(profile, 0);
                profile.OnAfterDeserialize();
                profile.OnAfterDeserialize();
                Assert.That(profile.Passes.OfType<MsvoPass>().Single(), Is.SameAs(node));
                Assert.That(node.MsvoIntensity, Is.EqualTo(2.3f));
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void PHNodesCompileWithIndependentLightFeatures(bool rectangles, bool rayTracing)
        {
            var profile = ScriptableObject.CreateInstance<TsukuyomiPipelineProfile>();
            try
            {
                // Reverse authoring order to verify the dependency, rather than relying on list order.
                if (rectangles) profile.Passes.Add(new RectLightPass());
                profile.Passes.Add(new GtaoPass());
                profile.Passes.Add(new MsvoPass());
                profile.Passes.Add(new SssSkinPass());
                if (rayTracing) profile.Passes.Add(new RayTracedShadowPass());
                using var session = new PassSession(profile, false);
                Assert.That(session.Valid, Is.True, string.Join("\n", session.Errors));
                if (rectangles && rayTracing)
                {
                    var upload = session.Passes.Single(p => p.Name == "Tsukuyomi Rect Lights");
                    Assert.That(upload.DependencyDescriptions.Any(d => d.Contains("Ray Traced Shadows")), Is.True);
                    var trace = session.Passes.Single(p => p.Name == "Tsukuyomi Ray Traced Shadows");
                    Assert.That(trace.Event, Is.EqualTo(RenderPassEvent.BeforeRenderingGbuffer - 1));
                    Assert.That(trace.Event, Is.LessThan(upload.Event));
                }
                // GTAO must prepare after MSVO even when the user places its node first.
                var order = (System.Collections.IEnumerable)typeof(PassSession).GetField("_prepareOrder", AllInstance).GetValue(session);
                var runtimes = order.Cast<object>().Select(o => o.GetType().GetField("Runtime", AllInstance).GetValue(o).GetType().Name).ToList();
                Assert.That(runtimes.IndexOf("TsukuyomiMultiScaleAmbientOcclusionPass"),
                    Is.LessThan(runtimes.IndexOf("TsukuyomiGroundTruthAmbientOcclusionPass")));
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void DisabledNodesAreAbsentEvenWithEnablingVolumeOverrides()
        {
            var profile = ScriptableObject.CreateInstance<TsukuyomiPipelineProfile>();
            try
            {
                profile.Passes.Add(new RectLightPass { Enabled = false });
                profile.Passes.Add(new RayTracedShadowPass { Enabled = false });
                profile.Passes.Add(new MsvoPass { Enabled = false });
                using var session = new PassSession(profile, false);
                Assert.That(session.Valid, Is.True);
                Assert.That(session.Passes, Is.Empty);
                var volume = ScriptableObject.CreateInstance<TsukuyomiMultiScaleAmbientOcclusionVolume>();
                try
                {
                    volume.enable.Override(true);
                    var type = typeof(MsvoPass).Assembly.GetType("Tsukuyomi.Rendering.TsukuyomiMultiScaleAmbientOcclusionResolvedSettings", true);
                    var settings = type.GetMethod("From").Invoke(null, new object[] { profile.Passes.OfType<MsvoPass>().Single(), volume });
                    Assert.That(type.GetField("Enabled").GetValue(settings), Is.False);
                }
                finally { UnityEngine.Object.DestroyImmediate(volume); }
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void DisposingSessionReleasesRayTracingRuntimeRegistration()
        {
            var type = typeof(RayTracedShadowPass).Assembly.GetType("Tsukuyomi.Rendering.TsukuyomiRayTracedShadowPass", true);
            var instances = type.GetField("s_Instances", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int Count() => (int)instances.GetType().GetProperty("Count").GetValue(instances);
            int before = Count();
            var profile = ScriptableObject.CreateInstance<TsukuyomiPipelineProfile>();
            try
            {
                profile.Passes.Add(new RayTracedShadowPass());
                var session = new PassSession(profile, false);
                Assert.That(Count(), Is.EqualTo(before + 1));
                session.Dispose(); session.Dispose();
                Assert.That(Count(), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
    }
}
