using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Compilation;

namespace Tsukuyomi.Rendering.Editor
{
    internal static class PassEditorTypes
    {
        private static HashSet<string> _players;
        internal static bool CanRunInPlayer(Type type)
        {
            _players ??= CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToHashSet();
            return _players.Contains(type.Assembly.GetName().Name);
        }
    }
}
