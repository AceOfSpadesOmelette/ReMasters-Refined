using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ReMastersLib
{
    public static class OutputManifestWriter
    {
        public static void Write(string outputPath)
        {
            var manifestDir = Path.Combine(outputPath, "manifest");
            Directory.CreateDirectory(manifestDir);
            var manifestPath = Path.Combine(manifestDir, "output_manifest.json");

            var entries = Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)
                .Select(p => new ManifestEntry
                {
                    Path = p.Substring(outputPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    SizeBytes = new FileInfo(p).Length
                })
                .OrderBy(e => e.Path)
                .ToList();

            var payload = new ManifestPayload { Files = entries };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(payload, Formatting.Indented));
        }

        private class ManifestPayload
        {
            public List<ManifestEntry> Files { get; set; }
        }

        private class ManifestEntry
        {
            public string Path { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
