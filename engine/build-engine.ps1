# Builds the DLSS 5 engine NeuralSwitch installs into games: OptiScaler-DLSS5 at a pinned commit, with
# the remote-control patch applied, staged into engine\dist.
#
#   powershell -ExecutionPolicy Bypass -File engine\build-engine.ps1
#
# Needs git and Visual Studio 2022 or 2026 (Build Tools are enough) with the "Desktop development with
# C++" workload. NVIDIA's nvngx_dlssnr.dll is not part of the build and is never downloaded here.

param(
    # Throw away engine\upstream and start from a fresh clone.
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$here = Split-Path -Parent $PSCommandPath
$src = Join-Path $here "upstream"
$dist = Join-Path $here "dist"
$commit = (Get-Content (Join-Path $here "UPSTREAM_COMMIT") -Raw).Trim()
$patches = Get-ChildItem (Join-Path $here "patches") -Filter *.patch | Sort-Object Name
$repo = "https://github.com/Skynizz/optiscaler-dlss5.git"

# Native tools write progress to stderr; under Windows PowerShell 5.1 with "Stop" that would be fatal,
# so they run with "Continue" and are judged by their exit code alone.
function Invoke-Native {
    param([string]$Exe, [string[]]$Arguments, [switch]$AllowFailure)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $Exe @Arguments 2>&1 | ForEach-Object { Write-Host "  $_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previous

    if ($code -ne 0 -and -not $AllowFailure) { throw "$Exe $($Arguments -join ' ') failed with exit code $code" }
    return $code
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"

    if (-not (Test-Path $vswhere)) {
        throw "Visual Studio was not found. Install Visual Studio 2022 or 2026 Build Tools with the 'Desktop development with C++' workload."
    }

    $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json | ConvertFrom-Json | Select-Object -First 1

    if ($null -eq $vs) {
        throw "Visual Studio is installed without the C++ tools. Add the 'Desktop development with C++' workload."
    }

    # The project names v143 (2022); 2026 ships v145 instead and builds it unchanged.
    $major = ([version]$vs.installationVersion).Major
    $toolset = if ($major -ge 18) { "v145" } else { "v143" }
    $msbuild = Join-Path $vs.installationPath "MSBuild\Current\Bin\MSBuild.exe"

    Write-Host "Visual Studio $($vs.installationVersion), toolset $toolset"
    return @{ Path = $msbuild; Toolset = $toolset }
}

# 1. The source at the pinned commit, with every submodule.
if ($Clean -and (Test-Path $src)) { Remove-Item $src -Recurse -Force }

if (-not (Test-Path (Join-Path $src ".git"))) {
    Invoke-Native git @("init", "-q", $src) | Out-Null
    Invoke-Native git @("-C", $src, "remote", "add", "origin", $repo) | Out-Null
}

$head = (& git -C $src rev-parse HEAD 2>$null)

if ($head -ne $commit) {
    Write-Host "Fetching OptiScaler-DLSS5 $commit"
    Invoke-Native git @("-C", $src, "fetch", "--depth", "1", "origin", $commit) | Out-Null
    Invoke-Native git @("-C", $src, "checkout", "-q", "--force", "FETCH_HEAD") | Out-Null
}

Write-Host "Updating submodules (the first run downloads several hundred MB)"
Invoke-Native git @("-C", $src, "submodule", "update", "--init", "--recursive", "--depth", "1") | Out-Null

# 2. The patches, each once. A patch that reverses cleanly is already in.
foreach ($patch in $patches) {
    if ((Invoke-Native git @("-C", $src, "apply", "--reverse", "--check", $patch.FullName) -AllowFailure) -eq 0) {
        Write-Host "Already applied: $($patch.Name)"
        continue
    }

    Invoke-Native git @("-C", $src, "apply", "--check", $patch.FullName) | Out-Null
    Invoke-Native git @("-C", $src, "apply", $patch.FullName) | Out-Null
    Write-Host "Applied: $($patch.Name)"
}

# 3. The forwarder first, on its own: the solution build does not reliably rebuild it.
$msbuild = Find-MSBuild
$common = @("/p:Configuration=Release", "/p:Platform=x64", "/p:PlatformToolset=$($msbuild.Toolset)", "/v:minimal", "/m", "/nologo")

foreach ($proj in @("OptiScaler\dlssnr\forwarder\dlssnr_forwarder.vcxproj", "OptiScaler.sln")) {
    Write-Host "Building $proj"
    Invoke-Native $msbuild.Path (@((Join-Path $src $proj)) + $common) | Out-Null
}

# 4. Stage exactly what an install needs.
$out = Join-Path $src "x64\Release\a"
$forwarder = Join-Path $src "OptiScaler\dlssnr\forwarder\x64\Release\a\nvngx.dll_dlssnr.dll"
if (-not (Test-Path $forwarder)) { $forwarder = Join-Path $out "nvngx.dll_dlssnr.dll" }

foreach ($required in @((Join-Path $out "OptiScaler.dll"), $forwarder)) {
    if (-not (Test-Path $required)) { throw "Build output missing: $required" }
}

# The patched engine says so in its own binary; a stale build without the pipe would install fine and
# then never answer.
$bytes = [System.IO.File]::ReadAllBytes((Join-Path $out "OptiScaler.dll"))
$utf16 = [System.Text.Encoding]::Unicode
# Read at both byte alignments, so the wide string is found wherever it sits.
$found = $utf16.GetString($bytes).Contains("neuralswitch-") -or $utf16.GetString($bytes, 1, $bytes.Length - 1).Contains("neuralswitch-")

if (-not $found) { throw "OptiScaler.dll was built without the remote-control patch." }

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

Copy-Item (Join-Path $out "OptiScaler.dll") $dist
Copy-Item $forwarder (Join-Path $dist "nvngx.dll_dlssnr.dll")
Copy-Item (Join-Path $src "OptiScaler.ini") $dist
Copy-Item (Join-Path $src "LICENSE") (Join-Path $dist "LICENSE-OptiScaler.txt")
# The build output's Licenses folder carries the third-party licences (DirectX, FidelityFX, XeSS) as well.
$licenses = @((Join-Path $out "Licenses"), (Join-Path $src "Licenses")) | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($licenses) { Copy-Item $licenses (Join-Path $dist "Licenses") -Recurse }

@{
    engine = "OptiScaler-DLSS5"
    upstream = $repo
    commit = $commit
    patches = @($patches | ForEach-Object { $_.Name })
    built = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json | Set-Content (Join-Path $dist "engine.json") -Encoding UTF8

Write-Host ""
Write-Host "Engine ready in $dist"
Get-ChildItem $dist -File | ForEach-Object { "  {0,-24} {1,10:N0} bytes" -f $_.Name, $_.Length }
