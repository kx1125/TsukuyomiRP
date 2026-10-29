using UnityEngine;

namespace Tsukuyomi.Rendering
{
    /// <summary>Optional module contracts. Providers own their configuration and cleanup.</summary>
    public interface IPcssShadowSettingsProvider
    {
        TsukuyomiPcssResolvedSettings ShadowSettings { get; }
    }
    public interface IContactShadowProvider
    {
        bool Active { get; }
        bool DenoiseActive { get; }
        void Deactivate(Camera camera);
    }
    public interface IPerObjectShadowProvider
    {
        bool Active { get; }
        void Deactivate(Camera camera);
    }
    public static class ShadowDependencies
    {
        public const string Contact = "tsukuyomi.contact-shadow";
        public const string ContactDenoised = "tsukuyomi.contact-shadow.denoised";
        public const string PerObject = "tsukuyomi.per-object-shadow";
        public static void RequireComposition(PassPlanBuilder plan)
            => plan.Service(TsukuyomiPcssScreenSpaceShadowPass.Key, () => new TsukuyomiPcssScreenSpaceShadowPass());
    }
}
