using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Tsukuyomi.Rendering.Editor
{
    // Missing serialized version fields must not be mistaken for the constructor's current default.
    internal static class PassProfileSchema
    {
        private static readonly Dictionary<string, (DateTime written, long length, int version)> Versions = new();
        internal static bool IsSupported(TsukuyomiPipelineProfile profile)
        {
            if (!profile || profile.SchemaVersion != TsukuyomiPipelineProfile.CurrentSchemaVersion) return false;
            string path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return true;
            var file = new FileInfo(path);
            if (!Versions.TryGetValue(path, out var entry) || entry.written != file.LastWriteTimeUtc || entry.length != file.Length)
            {
                var match = Regex.Match(File.ReadAllText(path), @"^  _schemaVersion:\s*(\d+)\s*$", RegexOptions.Multiline);
                int version = match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : 0;
                entry = (file.LastWriteTimeUtc, file.Length, version); Versions[path] = entry;
            }
            return entry.version == TsukuyomiPipelineProfile.CurrentSchemaVersion;
        }
    }
}
