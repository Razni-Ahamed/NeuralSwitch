# Assembles a release: the app, the CLI and the built engine in one folder, zipped.
#
#   powershell -ExecutionPolicy Bypass -File scripts\package.ps1
#
# Needs the engine built first (engine\build-engine.ps1). The app is published self-contained, so the
# zip runs on a PC without .NET installed. NVIDIA's nvngx_dlssnr.dll is never part of it.

param([string]$Version = "0.1.0")

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) "..")
$dist = Join-Path $root "engine\dist"
$out = Join-Path $root "artifacts\NeuralSwitch-$Version"
$zip = Join-Path $root "artifacts\NeuralSwitch-$Version-win-x64.zip"

if (-not (Test-Path (Join-Path $dist "OptiScaler.dll"))) { throw "Build the engine first: engine\build-engine.ps1" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null

$publish = @("-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true",
             "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
             "-p:Version=$Version", "-o", $out, "--nologo", "-v", "q")

foreach ($project in @("src\NeuralSwitch\NeuralSwitch.csproj", "src\NeuralSwitch.Cli\NeuralSwitch.Cli.csproj")) {
    & dotnet publish (Join-Path $root $project) @publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project failed" }
}

Get-ChildItem $out -Filter *.pdb | Remove-Item

# The app looks for the engine in an "engine" folder beside itself.
Copy-Item $dist (Join-Path $out "engine") -Recurse
# The documents, with docs\ beside them so the README's links work inside the zip too.
foreach ($doc in @("LICENSE", "README.md", "USER_CAUTION.md", "NOTICE.md", "THIRD_PARTY_NOTICES.md")) {
    Copy-Item (Join-Path $root $doc) $out
}
Copy-Item (Join-Path $root "docs") (Join-Path $out "docs") -Recurse

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out\*" -DestinationPath $zip -CompressionLevel Optimal

Write-Host "Release folder: $out"
Get-ChildItem $out -Recurse -File | ForEach-Object { "  {0,-40} {1,12:N0}" -f $_.FullName.Substring($out.Length + 1), $_.Length }
Write-Host ("Zip: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
