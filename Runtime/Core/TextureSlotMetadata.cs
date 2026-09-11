using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tsukuyomi.Rendering
{
    internal static class TextureSlotMetadata
    {
        private static readonly Dictionary<Type, MemberInfo[]> s_Members = new();

        // Cache reflection metadata, but read current slot values (some depend on feature settings).
        public static IEnumerable<TextureSlot> Enumerate(RenderPassBase pass)
        {
            Type type = pass.GetType();
            if (!s_Members.TryGetValue(type, out MemberInfo[] members))
            {
                var found = new List<MemberInfo>();
                var propertyNames = new HashSet<string>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (Type current = type; current != null; current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(flags))
                        if (field.FieldType == typeof(TextureSlot))
                            found.Add(field);
                    foreach (PropertyInfo property in current.GetProperties(flags))
                        if (property.PropertyType == typeof(TextureSlot) && property.GetGetMethod(true) != null
                            && property.GetIndexParameters().Length == 0 && propertyNames.Add(property.Name))
                            found.Add(property);
                }
                members = found.ToArray();
                s_Members.Add(type, members);
            }
            foreach (MemberInfo member in members)
                yield return member is FieldInfo field
                    ? (TextureSlot)field.GetValue(pass) : (TextureSlot)((PropertyInfo)member).GetValue(pass);
        }
    }
}
