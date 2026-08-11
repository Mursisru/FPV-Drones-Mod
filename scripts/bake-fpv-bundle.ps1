# Builds fpvmod_assets via Unity 2022.3 batchmode and rebuilds FPVMod.dll.
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",
    [string]$FbxSource = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$UnityProject = Join-Path $Root "FPVMod\UnityAssets"
$ModelsDir = Join-Path $UnityProject "Assets\Models"
$LogFile = Join-Path $UnityProject "bundle-build.log"
$DestFbx = Join-Path $ModelsDir "RPGBoD002fbx.fbx"

if (-not (Test-Path $UnityPath)) {
    throw "Unity not found: $UnityPath"
}

New-Item -ItemType Directory -Force -Path $ModelsDir | Out-Null

if ([string]::IsNullOrWhiteSpace($FbxSource)) {
    $candidates = @(
        (Join-Path $ModelsDir "RPGBoD002fbx.fbx"),
        (Join-Path $env:USERPROFILE "OneDrive\Documents\Blender\Drone\RPGBoD002fbx.fbx")
    )
    $docs = [Environment]::GetFolderPath('MyDocuments')
    if ($docs) { $candidates += (Join-Path $docs "Blender\Drone\RPGBoD002fbx.fbx") }
    foreach ($c in $candidates) {
        if (Test-Path $c) { $FbxSource = $c; break }
    }
}

if (-not [string]::IsNullOrWhiteSpace($FbxSource) -and (Test-Path $FbxSource) -and ($FbxSource -ne $DestFbx)) {
    Copy-Item -Force $FbxSource $DestFbx
    Write-Host "Copied FBX -> $DestFbx"
} elseif (-not (Test-Path $DestFbx)) {
    throw "FBX not found. Pass -FbxSource or place RPGBoD002fbx.fbx in $ModelsDir"
}

$texSources = @()
if (-not [string]::IsNullOrWhiteSpace($FbxSource) -and (Test-Path (Split-Path -Parent $FbxSource))) {
    $texSources += Split-Path -Parent $FbxSource
}
$docs = [Environment]::GetFolderPath('MyDocuments')
if ($docs) { $texSources += (Join-Path $docs "Blender\Drone") }
$texSources += (Join-Path $env:USERPROFILE "OneDrive\Documents\Blender\Drone")

foreach ($dir in ($texSources | Select-Object -Unique)) {
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem -Path $dir -Filter "*.png" -ErrorAction SilentlyContinue | ForEach-Object {
        $dest = Join-Path $ModelsDir $_.Name
        if ($_.FullName -eq $dest) { return }
        Copy-Item -Force $_.FullName $dest
        Write-Host "Copied texture -> $($_.Name)"
    }
}

$args = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $UnityProject,
    "-executeMethod", "FpvModBundleBuilder.BuildAll",
    "-logFile", $LogFile
)

Write-Host "Running Unity bundle bake..."
& $UnityPath @args | Out-Null

$bundle = Join-Path $Root "FPVMod\Resources\fpvmod_assets"
if (-not (Test-Path $bundle)) {
    if (Test-Path $LogFile) { Get-Content $LogFile -Tail 40 }
    throw "Bundle not produced: $bundle (Unity exit: $LASTEXITCODE)"
}

Write-Host "Bundle OK: $bundle"
dotnet build (Join-Path $Root "FPVMod\FPVMod.csproj") -c Release
Copy-Item -Force (Join-Path $Root "FPVMod\bin\Release\net48\FPVMod.dll") `
    "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\FPVMod.dll"
Write-Host "Deployed FPVMod.dll with embedded bundle."
