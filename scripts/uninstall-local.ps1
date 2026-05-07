$ErrorActionPreference = "Stop"
$installDir = Join-Path $env:LOCALAPPDATA "LightPilot\App"
$expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "LightPilot"))
$installFullPath = [System.IO.Path]::GetFullPath($installDir)

if (-not $installFullPath.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected path: $installFullPath"
}

Get-Process LightPilot.App -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -ErrorAction SilentlyContinue

$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Light Pilot.lnk"
Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

Write-Host "Uninstalled Light Pilot local app files."
