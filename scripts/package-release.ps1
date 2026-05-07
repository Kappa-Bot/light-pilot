param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifacts "LightPilot-$Version-$Runtime"
$zipPath = Join-Path $artifacts "LightPilot-$Version-$Runtime.zip"
$project = Join-Path $repoRoot "src\LightPilot.App\LightPilot.App.csproj"
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifacts)
$repoFullPath = [System.IO.Path]::GetFullPath($repoRoot)

if (-not $artifactsFullPath.StartsWith($repoFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write artifacts outside repository."
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue

dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $publishDir

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host $zipPath
