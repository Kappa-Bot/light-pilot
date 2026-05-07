$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet build LightPilot.sln
    dotnet test LightPilot.sln

    $installDir = Join-Path $env:LOCALAPPDATA "LightPilot\App"
    $exe = Join-Path $installDir "LightPilot.App.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Installed app not found: $exe"
    }

    $before = @(Get-Process LightPilot.App -ErrorAction SilentlyContinue).Count
    Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
    Start-Sleep -Seconds 2
    $after = @(Get-Process LightPilot.App -ErrorAction SilentlyContinue).Count
    if ($after -ne 1) {
        throw "Single-instance smoke failed. Before=$before After=$after"
    }

    $startup = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -ErrorAction Stop
    if ($startup.LightPilot -notlike '*--background*') {
        throw "Startup command missing --background"
    }

    Write-Host "Smoke OK"
}
finally {
    Pop-Location
}
