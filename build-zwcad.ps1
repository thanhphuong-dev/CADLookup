param(
    [string]$ZwcadManagedDir = "",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot ".artifacts\CADLookup.ZWCAD")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

if ($ZwcadManagedDir) {
    if (-not (Test-Path -LiteralPath (Join-Path $ZwcadManagedDir "ZwManaged.dll"))) {
        throw "ZWCAD managed API was not found: $((Join-Path $ZwcadManagedDir 'ZwManaged.dll'))"
    }
}

$project = Join-Path $root "src\CADLookup.Host.ZWCAD\CADLookup.Host.ZWCAD.csproj"
$buildArgs = @("build", $project, "-c", "Release")
if ($ZwcadManagedDir) { $buildArgs += "-p:ZwcadManagedDir=$ZwcadManagedDir" }
dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "CADLookup ZWCAD build failed with exit code $LASTEXITCODE" }

$buildOutput = Join-Path $root "src\CADLookup.Host.ZWCAD\bin\Release\net48"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $buildOutput -File -Filter "*.dll" |
    Copy-Item -Destination $OutputDirectory -Force
Copy-Item (Join-Path $root "deploy\ZWCAD\README.md") -Destination $OutputDirectory -Force
Write-Output "ZWCAD plugin staged at $OutputDirectory"
