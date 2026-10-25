# Loads the built engine (engine\dist) into a stand-in Direct3D 12 "game" and drives it over the
# remote-control pipe with the NeuralSwitch CLI, the way the app does. Needs the engine built
# (engine\build-engine.ps1), the CLI built (dotnet build), and the Visual Studio C++ tools.
#
#   powershell -ExecutionPolicy Bypass -File tools\engine-smoke-test\run-smoke-test.ps1
#
# A small window titled "NeuralSwitch engine smoke test" appears while it runs.

$ErrorActionPreference = "Stop"

$here = Split-Path -Parent $PSCommandPath
$root = Resolve-Path (Join-Path $here "..\..")
$dist = Join-Path $root "engine\dist"
$cli = @("Release", "Debug") | ForEach-Object { Join-Path $root "src\NeuralSwitch.Cli\bin\$_\net8.0-windows\neuralswitch-cli.exe" } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $cli) { $cli = "neuralswitch-cli.exe (not built)" }
$work = Join-Path ([System.IO.Path]::GetTempPath()) "neuralswitch-smoke"

foreach ($required in @((Join-Path $dist "OptiScaler.dll"), $cli)) {
    if (-not (Test-Path $required)) { throw "Missing $required. Build the engine and the solution first." }
}

# 1. Compile the stand-in game with the Visual Studio C++ tools.
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$vcvars = Join-Path $vs "VC\Auxiliary\Build\vcvars64.bat"

Get-Process smoke -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$work*" } | Stop-Process -Force
Start-Sleep -Milliseconds 300
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

$build = "`"$vcvars`" >nul && cl /nologo /O2 /EHsc /std:c++17 `"$(Join-Path $here 'smoke.cpp')`" /Fe:`"$work\smoke.exe`" /Fo:`"$work\\`""
cmd /c $build
if ($LASTEXITCODE -ne 0) { throw "Compiling smoke.cpp failed." }

# 2. Install the engine beside it the way the installer does, logging on.
Copy-Item (Join-Path $dist "OptiScaler.dll") (Join-Path $work "winmm.dll")
Copy-Item (Join-Path $dist "nvngx.dll_dlssnr.dll") $work
(Get-Content (Join-Path $dist "OptiScaler.ini") -Raw) -replace '(?m)^LogToFile=auto', 'LogToFile=true' -replace '(?m)^LogLevel=auto', 'LogLevel=2' |
    Set-Content (Join-Path $work "OptiScaler.ini") -NoNewline

# 3. Run it and talk to it.
$game = Start-Process -FilePath (Join-Path $work "smoke.exe") -ArgumentList "25" -WorkingDirectory $work -PassThru
$pipe = "neuralswitch-$($game.Id)"
$deadline = (Get-Date).AddSeconds(15)

while (-not (Test-Path "\\.\pipe\$pipe") -and (Get-Date) -lt $deadline -and -not $game.HasExited) {
    Start-Sleep -Milliseconds 200
}

$results = @()

function Step([string]$name, [string[]]$arguments, [string]$expect) {
    # The CLI reports refusals on stderr, which Windows PowerShell would otherwise make fatal.
    $ErrorActionPreference = "Continue"
    $out = (& $cli @arguments 2>&1 | ForEach-Object { "$_" } | Out-String).Trim()
    $ok = $out -match $expect
    $script:results += [pscustomobject]@{ Step = $name; Ok = $ok; Output = $out }
}

if (-not (Test-Path "\\.\pipe\$pipe")) {
    $results += [pscustomobject]@{ Step = "pipe appears"; Ok = $false; Output = "no \\.\pipe\$pipe after 15 s (exited: $($game.HasExited))" }
} else {
    $results += [pscustomobject]@{ Step = "pipe appears"; Ok = $true; Output = "\\.\pipe\$pipe" }
    Step "status"            @("status", "--pid", $game.Id)                  "smoke\.exe .*Neural Rendering off"
    Step "on"                @("on", "--pid", $game.Id)                      "smoke\.exe"
    Step "style natural"     @("style", "natural", "--pid", $game.Id)        "style natural"
    Step "intensity 0.8"     @("intensity", "0.8", "--pid", $game.Id)        "intensity 0\.80"
    Step "toggle"            @("toggle", "--pid", $game.Id)                  "Neural Rendering off"
    Step "rejects bad style" @("style", "vivid", "--pid", $game.Id)          "style takes"
    Step "still answering"   @("status", "--pid", $game.Id)                  "style natural, intensity 0\.80"
}

if (-not $game.HasExited) { Stop-Process -Id $game.Id -Force }

$log = Join-Path $work "OptiScaler.log"
$listening = (Test-Path $log) -and (Select-String -Path $log -Pattern "remote control listening" -Quiet)
$results += [pscustomobject]@{ Step = "log says listening"; Ok = $listening; Output = $log }

$results | Format-List
if ($results | Where-Object { -not $_.Ok }) { Write-Host "SMOKE TEST FAILED"; exit 1 }
Write-Host "SMOKE TEST PASSED"
