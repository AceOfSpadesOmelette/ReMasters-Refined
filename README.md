# ReMasters-Refined

`ReMasters-Refined` is a datamine/extraction tool for Pokemon Masters EX assets and metadata (Cocos2d-x based game data).  
It reads unpacked APK/OBB resources, decrypts/decompresses supported content, dumps outputs to your target folder, and supports:
- per-file extraction logging
- error log generation
- output manifest generation (hierarchy + byte sizes)
- KTX-to-PNG conversion and PLIST-based slicing workflow

## Requirements

- Windows (recommended for current workflow)
- .NET 8 SDK (for `ReMastersConsole`)
- PVRTexToolCLI executable (for KTX -> PNG conversion)
- Input data prepared from your PMEX files (APK + downloaded resources + shard)

NuGet dependencies are restored automatically by `dotnet restore/build`. Main library packages include:
- `Google.Protobuf`
- `Grpc` / `Grpc.Core` / `Grpc.Tools`
- `K4os.Compression.LZ4`
- `K4os.Hash.xxHash`
- `Newtonsoft.Json`
- `protobuf-net`
- `SixLabors.ImageSharp`
- `Waher.Security.ChaChaPoly`

## Usage

1. Open Command Prompt (or PowerShell).
2. Go to the console project:

```powershell
cd ReMastersConsole
```

3. Run the tool:

```powershell
dotnet run Program.cs
```

## Configuration Before Running (`Program.cs`)

Open `ReMastersConsole/Program.cs` and configure these sections:

### 1) `GameDataPaths`
Set all path values correctly for your local environment:
- `UnpackedAPKPath`
- `DownloadPath`
- `ShardPath`
- `OutputPath`
- `KTXConverterPath` (path to `PVRTexToolCLI.exe`)
- `RepositoryPath`

### 2) `DumpSettings`
Enable/disable extraction categories:
- `DumpStringsDL`
- `DumpStringsAPK`
- `DumpResources`
- `DumpSound`
- `DumpVideo`
- `DumpProto`
- `ConvertImages`
- `CopyResToBase`

### 3) Runtime outputs
At runtime, the tool will:
- print per-file extraction paths
- write an error log when failures occur
- generate an output manifest (file hierarchy + size in bytes)
- print total processing time before program exit

## Notes

- Ensure your input paths exist and are readable.
- Large runs can take significant time depending on enabled settings and disk speed.
- If using this for repeated datamines, compare manifest snapshots between runs to quickly locate changed files.

## Credits

Original work by [SciresM](https://github.com/SciresM/) and [Kaphotics](https://github.com/kwsch/).
