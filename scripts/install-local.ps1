param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA "LightPilot\App"
$project = Join-Path $repoRoot "src\LightPilot.App\LightPilot.App.csproj"

Get-Process LightPilot.App -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $installDir

$exe = Join-Path $installDir "LightPilot.App.exe"
$runCommand = '"' + $exe + '" --background'
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -Value $runCommand

$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "Light Pilot.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = $exe
$shortcut.Description = "Light Pilot"
$shortcut.Save()

Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
Write-Host "Installed Light Pilot to $installDir"
