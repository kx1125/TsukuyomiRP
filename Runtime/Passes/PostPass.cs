
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    [InjectionPoint(InjectionPoint.AfterPostProcess)]
    public abstract class PostPass : RenderPassBase
    {
        public override void Setup(in FrameContext frame) { }

        protected virtual TextureSlot SourceSlot => TextureSlot.Read("Source", BuiltinTexture.ActiveColor);
        protected virtual TextureSlot DestinationSlot => TextureSlot.Write(OutputName, BuiltinTexture.None);
        protected virtual string OutputName => $"{Name}Output";
        protected virtual bool ClearOutput => false;

        public TextureHandle RecordPost(in PostPassContext context, TextureHandle activeColor)
        {
            var source = context.GetTexture(SourceSlot);
            if (!source.IsValid())
                source = activeColor;

            var destination = context.CreateTextureLike(activeColor, OutputName, ClearOutput);
            if (!source.IsValid() || !destination.IsValid())
                return activeColor;

            int attachmentIndex = 0;
            context.BindTexture(source, SourceSlot, ref attachmentIndex);
            context.BindTexture(destination, DestinationSlot, ref attachmentIndex);

            context.PassData.source = source;
            context.PassData.destination = destination;

            Render(context, source, destination);
            return destination;
        }

        public abstract void Render(
            in PostPassContext context,
            TextureHandle source,
            TextureHandle destination);
    }
}
