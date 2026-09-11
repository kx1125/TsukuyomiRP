using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public enum TextureBinding
    {
        Auto,
        Texture,
        ColorAttachment,
        DepthAttachment
    }

    [System.Serializable]
    public readonly struct TextureSlot
    {
        public readonly string Name;
        public readonly ResourceAccess Access;
        public readonly BuiltinTexture Builtin;
        public readonly TextureDesc? CustomDesc;
        public readonly TextureBinding Binding;

        public bool IsBuiltin => Builtin != BuiltinTexture.None;

        public bool IsDepthAttachment => Binding == TextureBinding.DepthAttachment
            || (Binding == TextureBinding.Auto
                && (Builtin == BuiltinTexture.ActiveDepth || Builtin == BuiltinTexture.CameraDepthAttachment
                    || (Access != ResourceAccess.Read && CustomDesc.HasValue
                        && CustomDesc.Value.depthBufferBits != UnityEngine.Rendering.DepthBits.None)));

        public bool IsAttachment => IsDepthAttachment || Binding == TextureBinding.ColorAttachment
            || (Binding == TextureBinding.Auto &&
            (Access == ResourceAccess.Write || Access == ResourceAccess.ReadWrite) &&
            (Builtin == BuiltinTexture.ActiveColor ||
             Builtin == BuiltinTexture.CameraColorAttachment || 
             (Builtin == BuiltinTexture.None && (!CustomDesc.HasValue || !CustomDesc.Value.enableRandomWrite))));

        public bool RequiresIntermediateColor => Access != ResourceAccess.Write && !IsAttachment
            && (Builtin == BuiltinTexture.ActiveColor || Builtin == BuiltinTexture.CameraColorTexture
                || Builtin == BuiltinTexture.CameraColorAttachment);

        public TextureSlot(string name, ResourceAccess access, BuiltinTexture builtin, TextureBinding binding = TextureBinding.Auto)
        {
            Name = name;
            Access = access;
            Builtin = builtin;
            CustomDesc = null;
            Binding = binding;
        }

        public TextureSlot(string name, ResourceAccess access, TextureDesc desc, TextureBinding binding = TextureBinding.Auto)
        {
            Name = name;
            Access = access;
            Builtin = BuiltinTexture.None;
            CustomDesc = desc;
            Binding = binding;
        }

        public TextureSlot WithBinding(TextureBinding binding) => CustomDesc.HasValue
            ? new TextureSlot(Name, Access, CustomDesc.Value, binding)
            : new TextureSlot(Name, Access, Builtin, binding);

        // Factory Methods
        public static TextureSlot Read(string name, BuiltinTexture builtin) => new TextureSlot(name, ResourceAccess.Read, builtin);
        public static TextureSlot Read(string name, TextureDesc desc) => new TextureSlot(name, ResourceAccess.Read, desc);

        public static TextureSlot Write(string name, BuiltinTexture builtin) => new TextureSlot(name, ResourceAccess.Write, builtin);
        public static TextureSlot Write(string name, TextureDesc desc) => new TextureSlot(name, ResourceAccess.Write, desc);

        public static TextureSlot ReadWrite(string name, BuiltinTexture builtin) => new TextureSlot(name, ResourceAccess.ReadWrite, builtin);
        public static TextureSlot ReadWrite(string name, TextureDesc desc) => new TextureSlot(name, ResourceAccess.ReadWrite, desc);
    }
}
