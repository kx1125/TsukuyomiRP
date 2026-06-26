using System;

namespace Tsukuyomi.Rendering
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadAttribute : Attribute
    {
        public readonly BuiltinTexture Builtin;
        public readonly string CustomName;

        public ReadAttribute(BuiltinTexture builtin) => Builtin = builtin;
        public ReadAttribute(string name) => CustomName = name;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class WriteAttribute : Attribute
    {
        public readonly BuiltinTexture Builtin;
        public readonly string CustomName;

        public WriteAttribute(BuiltinTexture builtin)
        {
            Builtin = builtin;
        }

        public WriteAttribute(string name)
        {
            CustomName = name;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ReadWriteAttribute : Attribute
    {
        public readonly BuiltinTexture Builtin;
        public readonly string CustomName;

        public ReadWriteAttribute(BuiltinTexture builtin)
        {
            Builtin = builtin;
        }

        public ReadWriteAttribute(string name)
        {
            CustomName = name;
        }
    }
}
