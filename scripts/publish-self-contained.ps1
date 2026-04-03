# Self-contained publish (no .NET runtime required on target machine).
# Example: .\scripts\publish-self-contained.ps1
# Override RID: .\scripts\publish-self-contained.ps1 -Runtime win-arm64

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $root "ReMastersConsole\ReMastersConsole.csproj"
$out = Join-Path $root "publish\$Runtime"

dotnet publish $proj -c $Configuration -r $Runtime --self-contained true -o $out
