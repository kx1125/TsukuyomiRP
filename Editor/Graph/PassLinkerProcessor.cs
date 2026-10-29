using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;

namespace Tsukuyomi.Rendering.Editor
{
    // Includes third-party definitions without a central registry or handwritten link.xml.
    public sealed class PassLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;
        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data) => Generate();
        public static string Generate()
        {
            var players = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToHashSet();
            var xml = new StringBuilder("<linker>\n");
            foreach (var assembly in TypeCache.GetTypesDerivedFrom<RenderPassBase>()
                .Where(t => !t.IsAbstract && players.Contains(t.Assembly.GetName().Name))
                .GroupBy(t => t.Assembly.GetName().Name).OrderBy(g => g.Key))
            {
                xml.AppendLine($"  <assembly fullname=\"{SecurityElement.Escape(assembly.Key)}\" ignoreIfMissing=\"1\">");
                foreach (var type in assembly.OrderBy(t => t.FullName))
                    xml.AppendLine($"    <type fullname=\"{SecurityElement.Escape(type.FullName.Replace('+', '/'))}\" preserve=\"all\"/>");
                xml.AppendLine("  </assembly>");
            }
            xml.AppendLine("</linker>");
            const string path = "Library/Tsukuyomi/Passes.link.xml";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, xml.ToString()); return path;
        }
    }
}
