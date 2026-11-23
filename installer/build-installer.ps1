# Construit l'installateur : installer\Output\OKRegis-Setup-<version>.exe
# Prérequis : SDK .NET 10 et Inno Setup 6 (winget install JRSoftware.InnoSetup).
# Utilisation : powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$root = Split-Path $here -Parent
$stage = Join-Path $here "staging"
$cache = Join-Path $here "models-cache"

# 1. L'appli autonome
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet publish "$root\src\OkRegis.App" -c Release -r win-x64 --self-contained true -o "$stage\app" -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué" }

# 2. Les modèles vocaux (téléchargés une seule fois dans models-cache)
$models = @("vosk-model-small-fr-0.22", "vosk-model-small-en-us-0.15", "vosk-model-small-es-0.42")
New-Item -ItemType Directory -Force $cache | Out-Null
foreach ($m in $models) {
    if (-not (Test-Path "$cache\$m\am\final.mdl")) {
        Write-Host "Téléchargement de $m ..."
        $zip = Join-Path $cache "$m.zip"
        Invoke-WebRequest -Uri "https://alphacephei.com/vosk/models/$m.zip" -OutFile $zip -UseBasicParsing
        Expand-Archive -Path $zip -DestinationPath $cache -Force
        Remove-Item $zip
    }
    New-Item -ItemType Directory -Force "$stage\models" | Out-Null
    Copy-Item "$cache\$m" "$stage\models\$m" -Recurse
}

# 3. Inno Setup
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 introuvable (winget install JRSoftware.InnoSetup)" }
& $iscc "/DAppVersion=$Version" "$here\OkRegis.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC a échoué" }
Write-Host "Terminé : $here\Output\OKRegis-Setup-$Version.exe"
