param(
    [string]$AutoCADManagedDir = "C:\Program Files\Autodesk\AutoCAD 2023",
    [switch]$Install,
    [string]$InstallDirectory = (Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\CADLookup.bundle")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$output = Join-Path $root ".artifacts\CADLookup.bundle"
$contents = Join-Path $output "Contents\Windows"

$autoCadProject = Join-Path $root "src\CADLookup.Host.AutoCAD\CADLookup.Host.AutoCAD.csproj"
dotnet build $autoCadProject -c Release "-p:AutoCADManagedDir=$AutoCADManagedDir"
if ($LASTEXITCODE -ne 0) { throw "CADLookup build failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Path $contents -Force | Out-Null
$packageContents = Join-Path $root "deploy\CADLookup.bundle\PackageContents.xml"
Copy-Item $packageContents -Destination $output -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.txt") -Destination $output -Force
$buildOutput = Join-Path $root "src\CADLookup.Host.AutoCAD\bin\Release\net48"
Get-ChildItem $buildOutput -Filter "*.dll" -File |
    Where-Object { $_.Name -notin @("AcMgd.dll", "AcDbMgd.dll", "AcCoreMgd.dll", "AdWindows.dll") } |
    Copy-Item -Destination $contents -Force
Write-Output "Bundle staged at $output"

if ($Install) {
    $installContents = Join-Path $InstallDirectory "Contents\Windows"
    New-Item -ItemType Directory -Path $installContents -Force | Out-Null
    Copy-Item (Join-Path $output "PackageContents.xml") -Destination $InstallDirectory -Force
    Copy-Item (Join-Path $output "THIRD-PARTY-NOTICES.txt") -Destination $InstallDirectory -Force
    Copy-Item (Join-Path $contents "*.dll") -Destination $installContents -Force
    $smokeAssembly = Join-Path $installContents "CADLookup.Host.AutoCAD.Smoke.dll"
    if (Test-Path -LiteralPath $smokeAssembly) { Remove-Item -LiteralPath $smokeAssembly -Force }
    Write-Output "Bundle installed at $InstallDirectory. Restart AutoCAD before loading the updated plug-in."
}
