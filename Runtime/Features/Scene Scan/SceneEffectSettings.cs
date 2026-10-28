using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    internal static class SceneEffectSettings
    {
        private static T Value<T>(VolumeParameter<T> value, T fallback) => value != null && value.overrideState ? value.value : fallback;
        private static T Active<T>(VolumeStack stack) where T : VolumeComponent
        { var volume = stack?.GetComponent<T>(); return volume != null && volume.active ? volume : null; }
        private static void Coverage(TsukuyomiSceneEffectVolume volume, ref ScanCoverageParameters p)
        {
            p.Mode = Value(volume?.coverageMode, p.Mode);
            p.OriginWS = Value(volume?.originWS, p.OriginWS);
            p.Progress = Value(volume?.progress, p.Progress);
            p.Blend = Value(volume?.blend, p.Blend);
            p.WorldProgress = Value(volume?.worldProgress, p.WorldProgress);
            p.ScreenProgress = Value(volume?.screenProgress, p.ScreenProgress);
            p.MaxRange = Value(volume?.maxRange, p.MaxRange);
            p.RingWidth = Value(volume?.ringWidth, p.RingWidth);
            p.NoiseStrength = Value(volume?.noiseStrength, p.NoiseStrength);
        }
        internal static bool Resolve(VoxelTransitionPass profile, VolumeStack stack, out VoxelTransitionParameters p)
        {
            p = profile != null ? profile.VoxelTransition : VoxelTransitionParameters.Default;
            var volume = Active<TsukuyomiVoxelTransitionVolume>(stack);
            Coverage(volume, ref p.Coverage);
            p.CellSize = Value(volume?.cellSize, p.CellSize);
            p.SamplingStrength = Value(volume?.samplingStrength, p.SamplingStrength);
            p.DistortionStrength = Value(volume?.distortionStrength, p.DistortionStrength);
            p.SamplingCompleted = Value(volume?.samplingCompleted, p.SamplingCompleted);
            return Value(volume?.enable, profile != null && profile.Enabled);
        }
        internal static bool Resolve(ScanPostProcessPass profile, VolumeStack stack, out ScanPostProcessParameters p)
        {
            p = profile != null ? profile.ScanPostProcess : ScanPostProcessParameters.Default;
            var volume = Active<TsukuyomiScanPostProcessVolume>(stack);
            Coverage(volume, ref p.Coverage);
            p.CellSize = Value(volume?.cellSize, p.CellSize);
            p.Desaturation = Value(volume?.desaturation, p.Desaturation);
            p.TintStrength = Value(volume?.tintStrength, p.TintStrength);
            p.OutlineStrength = Value(volume?.outlineStrength, p.OutlineStrength);
            p.FresnelStrength = Value(volume?.fresnelStrength, p.FresnelStrength);
            p.SurfaceGridStrength = Value(volume?.surfaceGridStrength, p.SurfaceGridStrength);
            p.FlashStrength = Value(volume?.flashStrength, p.FlashStrength);
            p.PulseSpeed = Value(volume?.pulseSpeed, p.PulseSpeed);
            p.BasicIntensity = Value(volume?.basicIntensity, p.BasicIntensity);
            p.GridSubdivisions = Value(volume?.gridSubdivisions, p.GridSubdivisions);
            p.Flash = Value(volume?.flash, p.Flash);
            p.Reveal = Value(volume?.reveal, p.Reveal);
            p.Random01 = Value(volume?.random01, p.Random01);
            p.VoxelStrength = Value(volume?.voxelStrength, p.VoxelStrength);
            p.VoxelTransitionWeight = Value(volume?.voxelTransitionWeight, p.VoxelTransitionWeight);
            p.HorizontalGlitch = Value(volume?.horizontalGlitch, p.HorizontalGlitch);
            p.ChromaticSplit = Value(volume?.chromaticSplit, p.ChromaticSplit);
            p.GlitchStrength = Value(volume?.glitchStrength, p.GlitchStrength);
            p.AccentColor = Value(volume?.accentColor, p.AccentColor);
            p.TintColor = Value(volume?.tintColor, p.TintColor);
            p.DebugView = Value(volume?.debugView, p.DebugView);
            return Value(volume?.enable, profile != null && profile.Enabled);
        }
        internal static bool Resolve(XrayRenderPass profile, VolumeStack stack, out XrayRenderParameters p)
        {
            p = profile != null ? profile.XrayRender : XrayRenderParameters.Default;
            var volume = Active<TsukuyomiXrayRenderVolume>(stack);
            Coverage(volume, ref p.Coverage);
            p.CellSize = Value(volume?.cellSize, p.CellSize);
            p.Intensity = Value(volume?.intensity, p.Intensity);
            p.PrimaryColor = Value(volume?.primaryColor, p.PrimaryColor);
            p.SecondaryColor = Value(volume?.secondaryColor, p.SecondaryColor);
            return Value(volume?.enable, profile != null && profile.Enabled);
        }
        internal static bool Resolve(SpatialGridRenderPass profile, VolumeStack stack, out SpatialGridParameters p)
        {
            p = profile != null ? profile.SpatialGridRender : SpatialGridParameters.Default;
            var volume = Active<TsukuyomiSpatialGridRenderVolume>(stack);
            p.Coverage = (SpatialGridCoverage)Value(volume?.coverageMode, (ScanCoverageMode)p.Coverage);
            p.OriginWS = Value(volume?.originWS, p.OriginWS);
            p.TransitionProgress = Value(volume?.progress, p.TransitionProgress);
            p.Blend = Value(volume?.blend, p.Blend);
            p.WorldProgress = Value(volume?.worldProgress, p.WorldProgress);
            p.ScreenProgress = Value(volume?.screenProgress, p.ScreenProgress);
            p.MaxRange = Value(volume?.maxRange, p.MaxRange);
            p.RingWidth = Value(volume?.ringWidth, p.RingWidth);
            p.NoiseStrength = Value(volume?.noiseStrength, p.NoiseStrength);
            p.Spacing = Value(volume?.spacing, p.Spacing);
            p.Radius = Value(volume?.radius, p.Radius);
            p.LineWidth = Value(volume?.lineWidth, p.LineWidth);
            p.PointSize = Value(volume?.pointSize, p.PointSize);
            p.Intensity = Value(volume?.intensity, p.Intensity);
            p.Color = Value(volume?.color, p.Color);
            p.PulseSpeed = Value(volume?.pulseSpeed, p.PulseSpeed);
            return Value(volume?.enable, profile != null && profile.Enabled);
        }
        internal static ScanPostEffect Effects(ScanPostProcessPass profile, VolumeStack stack)
        {
            var effects = profile != null ? profile.ScanPostEffects : ScanPostEffect.All;
            var volume = Active<TsukuyomiScanPostProcessVolume>(stack);
            if (volume?.enableScan.overrideState == true) effects = volume.enableScan.value ? effects | ScanPostEffect.Scan : effects & ~ScanPostEffect.Scan;
            if (volume?.enableVoxelGlitch.overrideState == true) effects = volume.enableVoxelGlitch.value ? effects | ScanPostEffect.VoxelGlitch : effects & ~ScanPostEffect.VoxelGlitch;
            if (volume?.enableHorizontalGlitch.overrideState == true) effects = volume.enableHorizontalGlitch.value ? effects | ScanPostEffect.HorizontalGlitch : effects & ~ScanPostEffect.HorizontalGlitch;
            if (volume?.enableChromaticSplit.overrideState == true) effects = volume.enableChromaticSplit.value ? effects | ScanPostEffect.ChromaticSplit : effects & ~ScanPostEffect.ChromaticSplit;
            return effects;
        }
    }
}
