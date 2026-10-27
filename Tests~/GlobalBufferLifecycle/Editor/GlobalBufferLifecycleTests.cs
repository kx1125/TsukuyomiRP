using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tsukuyomi.Rendering.Tests
{
    public class GlobalBufferLifecycleTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        [TestCase("TsukuyomiRectLightPass", "s_EmptyBuffer", "_TsukuyomiRectLightCount")]
        [TestCase("TsukuyomiRayTracedShadowPass", "s_EmptyMap", "_TsukuyomiRTShadowCount")]
        public void ModeTransitionsKeepFallbackAlive(string typeName, string bufferField, string countName)
        {
            if (SystemInfo.graphicsShaderLevel < 45 || !SystemInfo.supports2DArrayTextures)
                Assert.Ignore("Structured buffers and texture arrays are required.");

            var type = typeof(TsukuyomiFeature).Assembly.GetType("Tsukuyomi.Rendering." + typeName, true);
            var callback = type.GetMethod("OnPlayModeStateChanged", Static);
            var clear = type.GetMethod("ClearGlobals", Static);
            var buffer = type.GetField(bufferField, Static);
            Assert.That(callback, Is.Not.Null);

            clear.Invoke(null, null);
            var original = (GraphicsBuffer)buffer.GetValue(null);
            Assert.That(original != null && original.IsValid(), Is.True);
            try
            {
                // These callbacks are also used with domain/scene reload disabled. There
                // must be no invalid SRV window while editor cameras are still drawing.
                foreach (var state in new[] { PlayModeStateChange.ExitingEditMode,
                    PlayModeStateChange.EnteredPlayMode, PlayModeStateChange.ExitingPlayMode,
                    PlayModeStateChange.EnteredEditMode })
                {
                    Shader.SetGlobalInt(countName, 1);
                    callback.Invoke(null, new object[] { state });
                    clear.Invoke(null, null);
                    Assert.That(original.IsValid(), Is.True, state + " disposed the fallback SRV");
                    Assert.That(buffer.GetValue(null), Is.SameAs(original), state.ToString());
                    Assert.That(Shader.GetGlobalInt(countName), Is.Zero, state.ToString());
                }

                if (typeName == "TsukuyomiRayTracedShadowPass")
                {
                    var sentinel = new int[1];
                    original.GetData(sentinel);
                    Assert.That(sentinel[0], Is.EqualTo(-1));
                    Assert.That(Shader.GetGlobalInt("_TsukuyomiRTShadowMainLightIndex"), Is.EqualTo(-1));
                    Assert.That(Shader.GetGlobalTexture("_TsukuyomiRTShadowTexture"), Is.Not.Null);
                }
                else
                {
                    var zeros = new float[original.stride / sizeof(float)];
                    original.GetData(zeros);
                    Assert.That(zeros, Is.All.Zero);
                }
            }
            finally
            {
                callback.Invoke(null, new object[] { PlayModeStateChange.EnteredEditMode });
            }
        }
    }
}
