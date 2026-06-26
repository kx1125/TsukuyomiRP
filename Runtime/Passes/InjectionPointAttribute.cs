using System;


namespace Tsukuyomi.Rendering
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class InjectionPointAttribute : Attribute
    {
        public InjectionPoint Point { get; }
        public InjectionPointAttribute(InjectionPoint point) => Point = point;
    }
}
