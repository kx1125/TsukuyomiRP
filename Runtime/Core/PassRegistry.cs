using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tsukuyomi.Rendering
{
    public class PassRegistry
    {
        private readonly List<RenderPassBase> _passes = new();

        public void AddPass(RenderPassBase pass)
        {
            if (!_passes.Contains(pass))
            {
                _passes.Add(pass);
            }
        }

        public IEnumerable<RenderPassBase> GetPasses(InjectionPoint injectionPoint)
        {
            return _passes
                .Where(p => p.Enabled && p.InjectionPoint == injectionPoint)
                .OrderByDescending(p => p.Priority);
        }

        public bool RequiresBuiltinTexture(BuiltinTexture builtin)
        {
            return _passes.Any(pass => pass.Enabled && PassRequiresBuiltinTexture(pass, builtin));
        }

        public void Clear()
        {
            _passes.Clear();
        }

        public static bool PassRequiresBuiltinTexture(RenderPassBase pass, BuiltinTexture builtin)
        {
            if (pass == null)
                return false;

            foreach (TextureSlot slot in EnumerateTextureSlots(pass))
            {
                if (slot.Builtin == builtin && slot.Access != ResourceAccess.Write)
                    return true;
            }

            return false;
        }

        private static IEnumerable<TextureSlot> EnumerateTextureSlots(RenderPassBase pass)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = pass.GetType();

            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType == typeof(TextureSlot))
                    yield return (TextureSlot)field.GetValue(pass);
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (property.PropertyType != typeof(TextureSlot) || property.GetIndexParameters().Length > 0)
                    continue;

                yield return (TextureSlot)property.GetValue(pass);
            }
        }
    }
}


