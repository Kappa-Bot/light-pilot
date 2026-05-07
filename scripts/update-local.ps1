param(
    [string]$Repository = "Kappa-Bot/light-pilot"
)

$ErrorActionPreference = "Stop"
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers @{ "User-Agent" = "LightPilot-Updater" }
$asset = $release.assets | Where-Object { $_.name -like "LightPilot-*-win-x64.zip" } | Select-Object -First 1
if ($null -eq $asset) {
    throw "No win-x64 ZIP asset found in latest release."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LightPilotUpdate-" + [guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot $asset.name
$extractPath = Join-Path $tempRoot "extract"
$installDir = Join-Path $env:LOCALAPPDATA "LightPilot\App"

function Stop-LightPilot {
    $processes = @(Get-Process LightPilot.App -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    $ids = $processes | ForEach-Object { $_.Id }
    $processes | Stop-Process -Force
    foreach ($id in $ids) {
        try {
            Wait-Process -Id $id -Timeout 10 -ErrorAction SilentlyContinue
        }
        catch {
        }
    }

    for ($attempt = 1; $attempt -le 20; $attempt++) {
        if (-not (Get-Process LightPilot.App -ErrorAction SilentlyContinue)) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "LightPilot.App.exe did not exit cleanly."
}

function Copy-WithRetry {
    param(
        [string]$Source,
        [string]$Destination
    )

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Copy-Item -Path $Source -Destination $Destination -Recurse -Force
            return
        }
        catch {
            if ($attempt -eq 5) {
                throw
            }

            Start-Sleep -Milliseconds (300 * $attempt)
        }
    }
}

New-Item -ItemType Directory -Path $tempRoot, $extractPath -Force | Out-Null
try {
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers @{ "User-Agent" = "LightPilot-Updater" }
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    Stop-LightPilot
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-WithRetry -Source (Join-Path $extractPath "*") -Destination $installDir

    $exe = Join-Path $installDir "LightPilot.App.exe"
    $runCommand = '"' + $exe + '" --background'
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -Value $runCommand
    Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
    Write-Host "Updated Light Pilot to $($release.tag_name)"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
