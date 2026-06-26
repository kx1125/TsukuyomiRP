using System.Collections.Generic;

using System.Linq;

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

        public void Clear()
        {
            _passes.Clear();
        }
    }
}
