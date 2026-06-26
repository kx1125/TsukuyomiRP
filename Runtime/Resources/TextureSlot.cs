using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    public readonly struct TextureSlot
    {
        public readonly string Name;
        public readonly ResourceAccess Access;
        public readonly BuiltinTexture Builtin;
        public readonly TextureDesc? CustomDesc;

        public bool IsBuiltin => Builtin != BuiltinTexture.None;

        public bool IsAttachment => 
            (Access == ResourceAccess.Write || Access == ResourceAccess.ReadWrite) &&
            (Builtin == BuiltinTexture.ActiveColor ||
             Builtin == BuiltinTexture.ActiveDepth ||
             Builtin == BuiltinTexture.CameraColorAttachment || 
             Builtin == BuiltinTexture.CameraDepthAttachment ||
             Builtin == BuiltinTexture.None);

        public TextureSlot(string name, ResourceAccess access, BuiltinTexture builtin)
        {
            Name = name;
            Access = access;
            Builtin = builtin;
            CustomDesc = null;
        }

        public TextureSlot(string name, ResourceAccess access, TextureDesc desc)
        {
            Name = name;
            Access = access;
            Builtin = BuiltinTexture.None;
            CustomDesc = desc;
        }

        // Factory Methods
        public static TextureSlot Read(string name, BuiltinTexture builtin) => new TextureSlot(name, ResourceAccess.Read, builtin);
        public static TextureSlot Read(string name, TextureDesc desc) => new TextureSlot(name, ResourceAccess.Read, desc);

        public static TextureSlot Write(string name, BuiltinTexture builtin) => new TextureSlot(name, ResourceAccess.Write, builtin);
        public static TextureSlot Write(string name, TextureDesc desc) => new TextureSlot(name, ResourceAccess.Write, desc);

        public static TextureSlot ReadWrite(string name, BuiltinTexture builtin) => new TextureSlot(name, ResourceAccess.ReadWrite, builtin);
        public static TextureSlot ReadWrite(string name, TextureDesc desc) => new TextureSlot(name, ResourceAccess.ReadWrite, desc);
    }
}
