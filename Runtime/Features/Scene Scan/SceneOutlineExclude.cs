using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    /// <summary>Excludes this hierarchy's visible surfaces from scene outlines on all cameras.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Rendering/Tsukuyomi RP/Scene Outline Exclude")]
    public sealed class SceneOutlineExclude : MonoBehaviour
    {
        private static readonly List<SceneOutlineExclude> Roots = new();
        internal static IReadOnlyList<SceneOutlineExclude> ActiveRoots => Roots;

        private void OnEnable() => Roots.Add(this);
        private void OnDisable() => Roots.Remove(this);
    }
}
