

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    [InjectionPoint(InjectionPoint.BeforeRendering)]
    [InjectionPoint(InjectionPoint.BeforeOpaque)]
    [InjectionPoint(InjectionPoint.AfterOpaque)]
    [InjectionPoint(InjectionPoint.BeforeSkybox)]
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    [InjectionPoint(InjectionPoint.AfterPostProcess)]
    [InjectionPoint(InjectionPoint.AfterRendering)]
    public abstract class RasterPass : RenderPassBase
    {
        public abstract void Record(in RasterPassContext context);
    }
}
