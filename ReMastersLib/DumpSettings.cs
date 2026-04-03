using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace ReMastersLib
{
    public class DumpSettings
    {
        public bool DumpStringsAPK { get; set; } = true;
        public bool DumpStringsDL { get; set; } = true;
        public bool DumpResources { get; set; } = true;
        public bool DumpSound { get; set; } = true;
        public bool DumpVideo { get; set; } = true;
        public bool DumpProto { get; set; } = true;
        public bool ConvertImages { get; set; } = true;
        public bool CopyResToBase { get; set; } = true;

        public readonly GameDataPaths Paths;

        public DumpSettings(GameDataPaths p) => Paths = p;

        public void DumpGameData()
        {
            ExtractionLogger.Initialize(Paths.OutputPath);

            var dumper = new GameDumper(Paths.UnpackedAPKPath, Paths.DownloadPath, Paths.KTXConverterPath);
            dumper.LoadShard(Paths.ShardPath);
            dumper.InitializeShardData(Paths.OutputPath);

            if (DumpStringsAPK)
            {
                Console.WriteLine("Dumping APK Messages...");
                dumper.DumpMessagesAPK(Paths.OutputPath);
            }

            if (DumpResources)
            {
                Console.WriteLine("Dumping Resources...");
                dumper.DumpResources(Paths.OutputPath);
            }

            if (DumpStringsDL)
            {
                Console.WriteLine("Dumping Download Messages...");
                dumper.DumpMessagesDownload(Paths.OutputPath);
            }

            if (DumpSound)
            {
                Console.WriteLine("Dumping Sounds...");
                dumper.DumpSound(Paths.OutputPath);
            }

            if (DumpVideo)
            {
                Console.WriteLine("Dumping Videos...");
                dumper.DumpVideo(Paths.OutputPath);
            }

            if (DumpProto)
            {
                Console.WriteLine("Dumping Protos...");
                dumper.DumpProto(Paths.OutputPath);
            }

            if (ConvertImages)
            {
                Console.WriteLine("Converting images...");
                ConvertKTX();
            }

            // if (CopyResToBase)
            // {
            //     Console.WriteLine("Copying resources to base...");
            //     CopyResourcesToBase();
            // }
        }

        public void ConvertKTX()
        {
            Console.WriteLine("Getting image and texture files list...");
            var outFiles = Directory.EnumerateFiles(Paths.OutputPath, "*.ktx", SearchOption.AllDirectories)
                .Select(fi => fi.Substring(Paths.OutputPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var outImages = Directory.EnumerateFiles(Paths.OutputPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg"))
                .Select(fi => fi.Substring(Paths.OutputPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var imagesRoot = Path.Combine(Paths.RepositoryPath, "images");
            var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Copy existing PNG and JPG files
            Console.WriteLine("Copying image files to output directory...");
            foreach (string relPath in outImages)
            {
                var src = Path.Combine(Paths.OutputPath, relPath);
                var dst = Path.Combine(imagesRoot, relPath);
                var outDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(outDir) && createdDirs.Add(outDir))
                    Directory.CreateDirectory(outDir);

                // Skip if destination is already newer (useful for re-runs)
                if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src))
                    continue;

                Console.WriteLine($"Extracting: {dst}");
                File.Copy(src, dst, true);
            }

            // Convert and copy KTX files
            Console.WriteLine("Converting and copying texture files to output directory...");
            foreach (string relPath in outFiles)
            {
                var src = Path.Combine(Paths.OutputPath, relPath);
                var fileName = Path.GetFileNameWithoutExtension(relPath) + ".png";
                var dst = Path.Combine(imagesRoot, Path.GetDirectoryName(relPath) ?? string.Empty, fileName);

                if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src))
                    continue;

                Console.WriteLine($"Extracting: {dst}");
                var dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir) && createdDirs.Add(dstDir))
                    Directory.CreateDirectory(dstDir);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = Paths.KTXConverterPath,
                    Arguments = $"-i \"{src}\" -ics sRGB -f r8g8b8a8 -d \"{dst}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    string outputResult = process.StandardOutput.ReadToEnd();
                    string errorResult = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"Error converting {src}: {errorResult}");
                    }
                    else
                    {
                        Console.WriteLine($"Successfully converted {src} to {dst}");
                    }
                }
            }
        }

        // public void CopyResourcesToBase()
        // {

        // }
    }
}