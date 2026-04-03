using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Grpc.Core.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using K4os.Hash.xxHash;
using ReMastersLib.Sound;

namespace ReMastersLib
{
    public class GameDumper
    {
        public readonly string BasePath;
        public readonly string DownloadPath;
        public readonly string KtxConverterPath;
        private Dictionary<string, ResourceLocationEntry> ResourceDB;
        private Dictionary<string, FileLocationEntry> FileDB;
        private ABND AssetShard;

        /// <summary>
        /// Initializes the asset unpacker
        /// </summary>
        /// <param name="root">Root folder where the unpacked apk data resides.</param>
        /// <param name="dlPath">Root folder where the download data resides.</param>
        public GameDumper(string root, string dlPath, string ktxConverterPath)
        {
            BasePath = root;
            DownloadPath = dlPath;
            KtxConverterPath = ktxConverterPath;
            // read in shards & file/folder maps
        }

        private void LoadResourceDB(string path)
        {
            var data = File.ReadAllBytes(path);
            var asdb = new ASDB(data);
            Debug.Assert(asdb.ExtHeader.EntrySize == 0x18);
            ResourceDB = asdb.GetEntryDictionary<ResourceLocationEntry>();
        }

        private void LoadFileDB(string path)
        {
            var data = File.ReadAllBytes(path);
            var asdb = new ASDB(data);
            Debug.Assert(asdb.ExtHeader.EntrySize == 0x10);
            FileDB = asdb.GetEntryDictionary<FileLocationEntry>();
        }

        public void LoadShard(string path)
        {
            var data = File.ReadAllBytes(path);
            AssetShard = new ABND(data);
        }

        public void InitializeShardData(string outRoot)
        {
            Directory.CreateDirectory(outRoot);
            // Process the asset database shard info, and load the unpacked files
            DumpABND(AssetShard, outRoot);
            LoadResourceDB(Path.Combine(outRoot, "resource_location.asdb"));
            LoadFileDB(Path.Combine(outRoot, "file_location.asdb"));
            // don't care about archive_info.asdb
            // don't care about bundle_header_hash.asdb
        }

        public void DumpMessagesAPK(string outRoot, string repoRoot = null, string websiteDataPath = null)
        {
            var messages = Path.Combine(BasePath, @"assets\resources\assets\Messages");
            var jsonPath = Path.Combine(outRoot, "lsddump", "apk");
            var txtPath = Path.Combine(outRoot, "lsddump", "lsd_apk.txt");
            LSDDumper.Dump(messages, jsonPath, txtPath);

            if(!Directory.Exists(jsonPath))
                return;

            if (repoRoot != null)
            {
                Console.WriteLine("Copying apk lsd files to repository...");
                Util.Copy(jsonPath, Path.Combine(repoRoot, "text", "lsddump", "apk"));
            }
            if (websiteDataPath != null)
            {
                Console.WriteLine("Copying apk lsd files to website data...");
                Util.Copy(jsonPath, Path.Combine(websiteDataPath, "lsd"), SearchOption.TopDirectoryOnly);
            }
        }

        public void DumpMessagesDownload(string outRoot, string repoRoot = null, string websiteDataPath = null)
        {
            var messages = Path.Combine(outRoot, "Messages");
            var jsonPath = Path.Combine(outRoot, "lsddump", "dl");
            var txtPath = Path.Combine(outRoot, "lsddump", "lsd_dl.txt");
            
            if(!Directory.Exists(messages))
                return;
            
            LSDDumper.Dump(messages, jsonPath, txtPath);

            if(!Directory.Exists(jsonPath))
                return;
            
            if (repoRoot != null)
            {
                Console.WriteLine("Copying dl lsd files to repository...");
                Util.Copy(jsonPath, Path.Combine(repoRoot, "text", "lsddump", "dl"));
            }
            if (websiteDataPath != null)
            {
                Console.WriteLine("Copying dl lsd files to website data...");
                Util.Copy(jsonPath, Path.Combine(websiteDataPath, "lsd"), SearchOption.TopDirectoryOnly);
            }
        }

        public void DumpResources(string outRoot)
        {
            // dump the abnd's, and track which ones we've already seen
            var processed = new HashSet<string>();
            
            foreach (var f in ResourceDB)
            {
                // Get the file's location on the disk
                var fn = f.Value.ContainerName;
                if (processed.Contains(fn))
                    continue;

                var folder = fn[0].ToString();
                var file = Path.Combine(DownloadPath, folder, fn);

                if (!File.Exists(file))
                {
                    Debug.WriteLine("Unable to find file (optional?): " + fn);
                    continue;
                }

                // Read ABND
                var data = File.ReadAllBytes(file);
                var abnd = new ABND(data);

                // Dump ABND to final location
                var outPath = outRoot; //  Path.Combine(outRoot, fn); -- don't bother using the ABND zip name; just get everything in one folder
                DumpABND(abnd, outPath);

                processed.Add(fn);
            }
            
            // Title & Splash Screens
            Util.Copy(Path.Combine(BasePath, "assets/resources/assets/ui/Title/image"), Path.Combine(outRoot, "ui/Title"));
        }

        public void DumpSound(string outRoot)
        {
            var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var audioType in new[] {"BGM", "SE", "Voice_en", "Voice_ja"})
            {
                var jsonPath = Path.Combine(outRoot, "sound", $"{audioType}.json");
                if (!File.Exists(jsonPath))
                    continue;

                var audioJson = JObject.Parse(File.ReadAllText(jsonPath));
                foreach (var audioBank in audioJson["audioBanks"])
                {
                    foreach (var audioResource in audioBank["audioResources"])
                    {
                        var name = audioResource["name"].ToString();
                        var format = audioResource["format"].ToString();
                        if (format != "ogg") throw new ArgumentException($"Unknown format for {name}: {format}");

                        var fileID = XXH64.DigestOf(Encoding.ASCII.GetBytes($"{name}.{format}"));
                        var folder = fileID;
                        while (folder >= 10)
                            folder /= 10;

                        var resourcePath = Path.Combine(DownloadPath, $"{folder}", $"{fileID}");

                        if (!File.Exists(resourcePath))
                        {
                            if (!name.StartsWith("sound/Bundle"))
                            {
                                Console.WriteLine($"Failed to find {name} ({resourcePath})");
                            }
                            continue;
                        }

                        var sf = new SoundFile(File.ReadAllBytes(resourcePath));

                        var outPath = $"{Path.Combine(outRoot, name.Replace("/", $"{Path.DirectorySeparatorChar}"))}.{format}";
                        Console.WriteLine($"Extracting: {outPath}");
                        try
                        {
                            var outDir = Path.GetDirectoryName(outPath);
                            if (!string.IsNullOrEmpty(outDir) && createdDirs.Add(outDir))
                                Directory.CreateDirectory(outDir);

                            File.WriteAllBytes(outPath, sf.Data);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed extracting: {outPath} ({ex})");
                            ExtractionLogger.Error($"Failed extracting {outPath}", ex);
                            throw;
                        }
                    }
                }
            }
        }

        private void DumpSyncPairPreviewVideo(string outRoot)
        {
            var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pbPath = Path.Combine(outRoot, @"db\master\pb\ScoutPickup.pb");

            if (!File.Exists(pbPath))
                pbPath = Path.Combine(outRoot, @"db\master\pb\LotteryPickup.pb"); // different name in v1.0.0

            if (!File.Exists(pbPath))
            {
                Console.WriteLine($"Failed to find ScoutPickup.pb or LotteryPickup.pb: skip Sync Pair Preview videos");
                return;
            }

            var table = ScoutPickupTable.Parser.ParseFrom(File.ReadAllBytes(pbPath));
            foreach (var message in table.Entries)
            {
                var path = $"Movie/Scout/Pickup/{message.ScoutId}/{message.ScoutPickupId}.mp4";
                var fileIDString = $"{XXH64.DigestOf(Encoding.ASCII.GetBytes(path))}";
                var resourcePath = Path.Combine(DownloadPath, $"{fileIDString[0]}", fileIDString);
                if (!File.Exists(resourcePath))
                {
                    Console.WriteLine($"Failed to find {path} ({resourcePath})");
                    continue;
                }

                var outPath = $"{Path.Combine(outRoot, path.Replace("/", $"{Path.DirectorySeparatorChar}"))}";
                Console.WriteLine($"Extracting: {outPath}");
                try
                {
                    var outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir) && createdDirs.Add(outDir))
                        Directory.CreateDirectory(outDir);

                    System.IO.File.Copy(resourcePath, outPath, true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed copying: {resourcePath} -> {outPath} ({ex})");
                    ExtractionLogger.Error($"Failed copying {resourcePath} -> {outPath}", ex);
                    throw;
                }
            }
        }

        private void DumpVideoWithUnknownName(string outRoot, Dictionary<string, string> unkNameVideos)
        {
            var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in FileDB.Values)
            {
                var fileIDString = $"{entry.FileID}";
                if (!unkNameVideos.ContainsKey(fileIDString))
                    continue;

                var resourcePath = Path.Combine(DownloadPath, $"{entry.FileName[0]}", entry.FileName);
                if (!File.Exists(resourcePath))
                {
                    Console.WriteLine($"Failed to find video {resourcePath}");
                    continue;
                }

                // we don't know the actual video names, but can still place them in the correct(ish?) folder
                var relPath = Path.GetDirectoryName(unkNameVideos[fileIDString]).Replace("/", $"{Path.DirectorySeparatorChar}");
                var outPath = $"{Path.Combine(outRoot, relPath, $"{entry.FileName}.mp4")}";
                Console.WriteLine($"Extracting: {outPath}");
                try
                {
                    var outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir) && createdDirs.Add(outDir))
                        Directory.CreateDirectory(outDir);

                    System.IO.File.Copy(resourcePath, outPath, true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed copying: {resourcePath} -> {outPath} ({ex})");
                    ExtractionLogger.Error($"Failed copying {resourcePath} -> {outPath}", ex);
                    throw;
                }
            }
        }

        public void DumpMiscVideo(string outRoot)
        {
            var jsonPath = Path.Combine(outRoot, @"db\asset\bundles_archives.json");
            if (!File.Exists(jsonPath))
                return;

            var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unkNameVideos = new Dictionary<string, string>();
            var json = JObject.Parse(File.ReadAllText(jsonPath));
            foreach (var archive in json["archives"])
            {
                var archiveName = archive["name"].ToString();
                if (!archiveName.StartsWith("archive_Movie_"))
                    continue;

                foreach (var p in archive["include"])
                {
                    var path = p.ToString();
                    if (path.EndsWith(".mp4"))
                    {
                        var fileIDString = $"{XXH64.DigestOf(Encoding.ASCII.GetBytes(path))}";
                        var resourcePath = Path.Combine(DownloadPath, $"{fileIDString[0]}", fileIDString);

                        if (!File.Exists(resourcePath))
                        {
                            Console.WriteLine($"Failed to find {path} ({resourcePath})");
                            continue;
                        }

                        var outPath = $"{Path.Combine(outRoot, path.Replace("/", $"{Path.DirectorySeparatorChar}"))}";
                        Console.WriteLine($"Extracting: {outPath}");
                        try
                        {
                            var outDir = Path.GetDirectoryName(outPath);
                            if (!string.IsNullOrEmpty(outDir) && createdDirs.Add(outDir))
                                Directory.CreateDirectory(outDir);

                            System.IO.File.Copy(resourcePath, outPath, true);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed copying: {resourcePath} -> {outPath} ({ex})");
                            ExtractionLogger.Error($"Failed copying {resourcePath} -> {outPath}", ex);
                            throw;
                        }
                    }
                    else if (!path.StartsWith("Movie/Scout/Pickup/"))
                    {
                        var archiveNameHashString = $"{XXH64.DigestOf(Encoding.ASCII.GetBytes(archiveName))}";
                        if (unkNameVideos.ContainsKey(archiveNameHashString))
                            Console.WriteLine($"Multiple paths given for {archiveName}: fallback to {path}");
                        unkNameVideos[archiveNameHashString] = path;
                    }
                }
            }
            DumpVideoWithUnknownName(outRoot, unkNameVideos);
        }

        public void DumpVideo(string outRoot)
        {
            DumpSyncPairPreviewVideo(outRoot);
            DumpMiscVideo(outRoot);
        }

        public void DumpProto(string outRoot, string repoRoot = "",
            bool tableLayout = true)
        {
            var pdf = Path.Combine(outRoot, "protodump");
            Directory.CreateDirectory(pdf);

            var types = ProtoTableDumper.GetProtoTypes();
            foreach (var t in types)
            {
                var name = t.Name.Replace("Table", string.Empty);
                var filename = $"{name}.pb";
                var path = Path.Combine(outRoot, @"db\master\pb\", filename);
                var outpath = Path.Combine(pdf, $"{name}.json");
                if (!File.Exists(path))
                {
                    Debug.WriteLine($"Couldn't find proto data file: {name}");
                    continue;
                }

                var data = File.ReadAllBytes(path);

                if (tableLayout)
                {
                    var result = ProtoTableDumper.GetProtoStrings(t, data);
                    if (result == null)
                    {
                        Debug.WriteLine($"Bad conversion for {name}, skipping.");
                        continue;
                    }

                    Console.WriteLine($"Extracting: {outpath}");
                    try
                    {
                        File.WriteAllLines(outpath, result);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed extracting: {outpath} ({ex})");
                        ExtractionLogger.Error($"Failed extracting {outpath}", ex);
                        throw;
                    }
                }
                else
                {
                    var result = ProtoTableDumper.GetProtoString(t, data);
                    if (result == null)
                    {
                        Debug.WriteLine($"Bad conversion for {name}, skipping.");
                        continue;
                    }

                    Console.WriteLine($"Extracting: {outpath}");
                    try
                    {
                        File.WriteAllText(outpath, result);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed extracting: {outpath} ({ex})");
                        ExtractionLogger.Error($"Failed extracting {outpath}", ex);
                        throw;
                    }
                }
            }

            if (repoRoot != null)
            {
                Console.WriteLine("Copying dumped proto files to repository...");
                Util.Copy(Path.Combine(outRoot, "protodump"), Path.Combine(repoRoot, "text", "protodump"));
            }
        }

        /// <summary>
        /// Spits out all contents of the <see cref="abnd"/> to the <see cref="outPath"/>
        /// </summary>
        /// <param name="abnd">Archive</param>
        /// <param name="outPath">Location to dump to</param>
        private void DumpABND(ABND abnd, string outPath)
        {
            var createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(outPath);
            for (int i = 0; i < abnd.ExtHeader.FileCount; i++)
            {
                var fi = abnd.GetFileInfo(i);
                var fn = abnd.GetFileName(fi);
                var fd = abnd.GetFileData(fi);

                var path = Path.Combine(outPath, fn);
                Console.WriteLine($"Extracting: {path}");
                try
                {
                    var di = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(di) && createdDirs.Add(di))
                        Directory.CreateDirectory(di);

                    File.WriteAllBytes(path, fd);
                    ConvertKtxInPlace(path);
                    SlicePlistIfReady(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed extracting: {path} ({ex})");
                    ExtractionLogger.Error($"Failed extracting {path}", ex);
                    throw;
                }
            }
        }

        private void ConvertKtxInPlace(string path)
        {
            if (!path.EndsWith(".ktx", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(KtxConverterPath) || !File.Exists(KtxConverterPath))
            {
                ExtractionLogger.Error($"KTX converter not found: {KtxConverterPath}");
                return;
            }

            var pngPath = Path.ChangeExtension(path, ".png");
            if (File.Exists(pngPath) && File.GetLastWriteTimeUtc(pngPath) >= File.GetLastWriteTimeUtc(path))
                return;

            Console.WriteLine($"Extracting: {pngPath}");
            var psi = new ProcessStartInfo
            {
                FileName = KtxConverterPath,
                Arguments = $"-i \"{path}\" -ics sRGB -f r8g8b8a8 -d \"{pngPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    var msg = $"Error converting {path} to {pngPath}: {error}";
                    Console.Error.WriteLine(msg);
                    ExtractionLogger.Error(msg);
                }
            }
        }

        private void SlicePlistIfReady(string path)
        {
            string plistPath = null;
            string pngPath = null;

            if (path.EndsWith(".plist", StringComparison.OrdinalIgnoreCase))
            {
                plistPath = path;
                pngPath = Path.ChangeExtension(path, ".png");
            }
            else if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                pngPath = path;
                plistPath = Path.ChangeExtension(path, ".plist");
            }

            if (string.IsNullOrEmpty(plistPath) || !File.Exists(plistPath) || !File.Exists(pngPath))
                return;

            try
            {
                PlistSpriteSlicer.Slice(plistPath, pngPath);
            }
            catch (Exception ex)
            {
                ExtractionLogger.Error($"Failed slicing plist {plistPath} with texture {pngPath}", ex);
            }
        }
    }
}