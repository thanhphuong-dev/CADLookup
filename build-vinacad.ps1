param(
    [string]$VinaCADManagedDir = "C:\Program Files\VinaCAD\VinaCAD 2026",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot ".artifacts\CADLookup.VinaCAD")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$coreApi = Join-Path $VinaCADManagedDir "VinaCADCoreMgd_Net8.dll"
$teighaApi = Join-Path $VinaCADManagedDir "TD_Mgd_Net8_26.12_17.dll"
foreach ($sdkFile in @($coreApi, $teighaApi)) {
    if (-not (Test-Path -LiteralPath $sdkFile)) { throw "VinaCAD SDK assembly not found: $sdkFile" }
}

$project = Join-Path $root "src\CADLookup.Host.VinaCAD\CADLookup.Host.VinaCAD.csproj"
dotnet build $project -c Release "-p:VinaCADManagedDir=$VinaCADManagedDir"
if ($LASTEXITCODE -ne 0) { throw "CADLookup VinaCAD build failed with exit code $LASTEXITCODE" }

$buildOutput = Join-Path $root "src\CADLookup.Host.VinaCAD\bin\Release\net8.0-windows"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $buildOutput -File | Where-Object { $_.Extension -in @(".dll", ".json") } |
    Copy-Item -Destination $OutputDirectory -Force
Copy-Item (Join-Path $root "deploy\VinaCAD\README.md") -Destination $OutputDirectory -Force
Write-Output "VinaCAD plugin staged at $OutputDirectory"
