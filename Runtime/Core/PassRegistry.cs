using System.Collections.Generic;

namespace Tsukuyomi.Rendering
{
    public class PassRegistry
    {
        private readonly List<RenderPassBase> _passes = new();
        private readonly List<PassState> _states = new();
        private readonly Dictionary<InjectionPoint, List<RenderPassBase>> _groups = new();
        private bool _dirty;

        public void AddPass(RenderPassBase pass)
        {
            if (pass != null && !_passes.Contains(pass))
            {
                _passes.Add(pass);
                _dirty = true;
            }
        }

        /// <summary>Synchronize additions, removals and ordering, including edits to an existing list.</summary>
        public void Synchronize(IReadOnlyList<RenderPassBase> passes)
        {
            int index = 0;
            bool changed = false;
            if (passes != null)
            {
                for (int i = 0; i < passes.Count; i++)
                {
                    if (passes[i] == null) continue;
                    if (index >= _passes.Count || !ReferenceEquals(passes[i], _passes[index]))
                        changed = true;
                    index++;
                }
            }
            if (!changed && index == _passes.Count)
                return;

            Clear();
            if (passes != null)
                for (int i = 0; i < passes.Count; i++)
                    AddPass(passes[i]);
        }

        public IReadOnlyList<RenderPassBase> GetPasses(InjectionPoint injectionPoint)
        {
            RefreshGroups();
            return _groups.TryGetValue(injectionPoint, out List<RenderPassBase> passes)
                ? passes : System.Array.Empty<RenderPassBase>();
        }

        private void RefreshGroups()
        {
            bool changed = _dirty || _states.Count != _passes.Count;
            if (!changed)
                for (int i = 0; i < _passes.Count; i++)
                    changed |= !_states[i].Matches(_passes[i]);
            if (!changed) return;

            foreach (List<RenderPassBase> group in _groups.Values)
                group.Clear();
            _states.Clear();
            foreach (RenderPassBase pass in _passes)
            {
                _states.Add(new PassState(pass));
                if (!pass.Enabled) continue;
                if (!_groups.TryGetValue(pass.InjectionPoint, out List<RenderPassBase> group))
                {
                    group = new List<RenderPassBase>();
                    _groups.Add(pass.InjectionPoint, group);
                }
                // Stable priority order: equal priorities preserve the Profile/Bake order.
                int insert = group.Count;
                while (insert > 0 && group[insert - 1].Priority < pass.Priority)
                    insert--;
                group.Insert(insert, pass);
            }
            _dirty = false;
        }

        public bool RequiresBuiltinTexture(BuiltinTexture builtin)
        {
            foreach (RenderPassBase pass in _passes)
                if (pass.Enabled && PassRequiresBuiltinTexture(pass, builtin))
                    return true;
            return false;
        }

        public void Clear()
        {
            _passes.Clear();
            _dirty = true;
        }

        public static bool PassRequiresBuiltinTexture(RenderPassBase pass, BuiltinTexture builtin)
        {
            if (pass == null)
                return false;

            foreach (TextureSlot slot in TextureSlotMetadata.Enumerate(pass))
            {
                if (slot.Builtin == builtin && slot.Access != ResourceAccess.Write)
                    return true;
            }

            return false;
        }

        private readonly struct PassState
        {
            private readonly bool _enabled;
            private readonly InjectionPoint _point;
            private readonly int _priority;

            public PassState(RenderPassBase pass)
            {
                _enabled = pass.Enabled;
                _point = pass.InjectionPoint;
                _priority = pass.Priority;
            }

            public bool Matches(RenderPassBase pass) => _enabled == pass.Enabled
                && _point == pass.InjectionPoint && _priority == pass.Priority;
        }
    }
}

