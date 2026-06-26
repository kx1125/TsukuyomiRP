using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public readonly struct BufferSlot
    {
        public readonly string Name;
        public readonly ResourceAccess Access;
        public readonly BufferDesc? CustomDesc;

        public BufferSlot(string name, ResourceAccess access, BufferDesc desc)
        {
            Name = name;
            Access = access;
            CustomDesc = desc;
        }

        public static BufferSlot Read(string name, BufferDesc desc) => new BufferSlot(name, ResourceAccess.Read, desc);
        public static BufferSlot Write(string name, BufferDesc desc) => new BufferSlot(name, ResourceAccess.Write, desc);
        public static BufferSlot ReadWrite(string name, BufferDesc desc) => new BufferSlot(name, ResourceAccess.ReadWrite, desc);
    }
}
