using System;
using System.Diagnostics;
using System.IO;
using ReMastersLib;

namespace ReMastersConsole
{
    public static class Program
    {
        private static void Main(string[] args)
        {

            var paths = new GameDataPaths
            {

                // Paths for the single game version
                UnpackedAPKPath = @"C:\Users\user\Downloads\PMEX\PMEX DM\APK",
                DownloadPath = @"C:\Users\user\Downloads\PMEX\PMEX DM\OBB\downloaded-resource-dir",
                ShardPath = @"C:\Users\user\Downloads\PMEX\PMEX DM\OBB\downloaded-resource-dir\assetdb_shard",
                
                OutputPath = @"C:\Users\user\Downloads\PMEX\PMEX DM\OUTPUT",

                KTXConverterPath = @"C:\Users\user\Downloads\PMEX\ReMasters-custom\PVRTexToolCLI.exe",

                RepositoryPath = @"C:\Users\user\Downloads\PMEX\PMEX DM\REPO",
            };

            // Create the output directory
            Directory.CreateDirectory(paths.OutputPath);

            // Configure settings for dumping game data
            var settings = new DumpSettings(paths)
            {
                DumpStringsDL = true,
                DumpStringsAPK = true,

                DumpResources = true,
                DumpSound = false,
                DumpVideo = false,
                DumpProto = true,

                ConvertImages = true,
                CopyResToBase = true,
            };

            var sw = Stopwatch.StartNew();
            try
            {
                settings.DumpGameData();
            }
            catch (Exception ex)
            {
                ExtractionLogger.Error("Unhandled extraction error", ex);
                throw;
            }
            finally
            {
                try
                {
                    OutputManifestWriter.Write(paths.OutputPath);
                }
                catch (Exception ex)
                {
                    ExtractionLogger.Error("Failed generating output manifest", ex);
                }

                sw.Stop();
                Console.WriteLine($"Total processing time: {sw.Elapsed}");
            }
        }
    }
}