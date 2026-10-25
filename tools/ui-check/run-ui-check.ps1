# Drives the real NeuralSwitch window with Windows UI Automation, the way a person would, against a
# simulated game, and checks after every step that the game actually received the change.
#
#   powershell -ExecutionPolicy Bypass -File tools\ui-check\run-ui-check.ps1
#
# Needs the solution built (dotnet build -c Release) and the engine built (engine\build-engine.ps1).
# Uses a throwaway settings file and a fake game in %TEMP%; your own settings and games are not touched.
# The NeuralSwitch window appears while it runs. Don't use the mouse or keyboard until it finishes.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms

$root = Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) "..\..")
function Built([string]$project, [string]$exe) {
    foreach ($config in "Release", "Debug") {
        $path = Join-Path $root "src\$project\bin\$config\net8.0-windows\$exe"
        if (Test-Path $path) { return $path }
    }
    throw "Build the solution first: $exe not found."
}
$app = Built "NeuralSwitch" "NeuralSwitch.exe"
$cli = Built "NeuralSwitch.Cli" "neuralswitch-cli.exe"

# A throwaway world: settings, a fake game, and a fake (unsigned) model.
$work = Join-Path ([IO.Path]::GetTempPath()) "neuralswitch-ui-check"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
$game = New-Item -ItemType Directory -Path (Join-Path $work "library\Fake Game")
Copy-Item "$env:SystemRoot\System32\notepad.exe" (Join-Path $game "FakeGame.exe")
New-Item -ItemType File -Path (Join-Path $game "nvngx_dlss.dll") | Out-Null
$model = New-Item -ItemType Directory -Path (Join-Path $work "model")
Set-Content (Join-Path $model "nvngx_dlssnr.dll") "not really a model"
$settings = Join-Path $work "settings.json"
@{
    LibraryFolders = @((Join-Path $work "library"))
    ModelPath = (Join-Path $model "nvngx_dlssnr.dll")
    CloseToTray = $false
} | ConvertTo-Json | Set-Content $settings -Encoding UTF8
$gameFilesBefore = (Get-ChildItem $game | Sort-Object Name | ForEach-Object Name) -join ","

$results = New-Object System.Collections.Generic.List[object]
function Check([string]$step, [bool]$ok, [string]$detail = "") {
    $results.Add([pscustomobject]@{ Ok = $ok; Step = $step; Detail = $detail })
    $mark = if ($ok) { "PASS" } else { "FAIL" }
    Write-Host ("{0}  {1}  {2}" -f $mark, $step, $detail)
}

$AE = [System.Windows.Automation.AutomationElement]
# Script blocks see their caller's variables (dynamic scope), so the names here must not shadow the callers'.
function Wait-Until([scriptblock]$probe, [double]$seconds = 10) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        try { $value = & $probe; if ($value) { return $value } } catch { }
        Start-Sleep -Milliseconds 150
    }
    return $null
}
function Find([string]$id, [double]$seconds = 10) {
    $byId = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    Wait-Until { $script:window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId) } $seconds
}
function Gone([string]$id) {
    $byId = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    [bool](Wait-Until { -not $script:window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId) } 10)
}
function Click($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Text([string]$id) { $e = Find $id 3; if ($e) { $e.Current.Name } else { "" } }
function GameStatus { (& $cli status --pid $script:sim.Id 2>&1 | Out-String).Trim() }
function Wait-Status([string]$pattern) { [bool](Wait-Until { (GameStatus) -match $pattern } 8) }

$sim = $null
$proc = $null

try {
    # The simulated game, and the app.
    $sim = Start-Process $cli -ArgumentList "simulate", "--for", "300" -PassThru -WindowStyle Hidden
    $proc = Start-Process $app -ArgumentList "--settings", "`"$settings`"" -PassThru
    $pidCondition = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
    $script:window = Wait-Until { $AE::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCondition) } 20
    Check "app window opens" ([bool]$window)
    if (-not $window) { throw "no window" }

    # First run: the caution must be accepted before anything else.
    $continue = Find "CautionContinue"
    Check "caution shown, Continue disabled until accepted" ($continue -and -not $continue.Current.IsEnabled)
    (Find "CautionAccept").GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Check "Continue enabled after ticking the box" ([bool](Wait-Until { (Find "CautionContinue").Current.IsEnabled } 3))
    Click (Find "CautionContinue")
    Check "caution closes" (Gone "CautionOverlay")
    Check "acceptance saved" ((Get-Content $settings -Raw) -match '"AcceptedCaution":\s*1')

    # Now playing, connected to the simulated game.
    Check "connects to the running game" ([bool](Wait-Until { (Text "GameTitle") -eq "SimulatedGame" } 10)) (Text "GameTitle")
    Check "status shows Off" ((Text "StatusTitle") -eq "Off") (Text "StatusTitle")

    Click (Find "PowerSwitch")
    Check "switch turns Neural Rendering on in the game" (Wait-Status "running") (GameStatus)
    Check "status shows Running" ([bool](Wait-Until { (Text "StatusTitle") -eq "Running" } 5)) (Text "StatusTitle")

    Click (Find "Style_Cinematic")
    Check "Cinematic reaches the game" (Wait-Status "style cinematic") (GameStatus)
    Click (Find "Style_Natural")
    Check "Natural reaches the game" (Wait-Status "style natural") (GameStatus)

    (Find "IntensitySlider").GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(0.5)
    Check "intensity slider reaches the game" (Wait-Status "intensity 0\.50") (GameStatus)
    Check "intensity shown as 0.50" ((Text "IntensityValue") -eq "0.50") (Text "IntensityValue")

    Click (Find "PowerSwitch")
    Check "switch turns it off again" (Wait-Status "Neural Rendering off") (GameStatus)

    # The global hotkey, only when this instance owns it (otherwise the keys would go to another window).
    Click (Find "Nav_Settings")
    if ((Text "HotkeyStatus") -match "active") {
        [System.Windows.Forms.SendKeys]::SendWait("^%n")
        Check "Ctrl+Alt+N turns it on" (Wait-Status "running") (GameStatus)
        [System.Windows.Forms.SendKeys]::SendWait("^%n")
        Check "Ctrl+Alt+N turns it off" (Wait-Status "Neural Rendering off") (GameStatus)
    } else {
        Check "hotkeys registered" $false (Text "HotkeyStatus")
    }

    # Settings.
    (Find "ToggleKeyBox").GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("K")
    Click (Find "ApplyHotkeysButton")
    Check "new hotkey saved" ([bool](Wait-Until { (Get-Content $settings -Raw) -match '"ToggleHotkey":\s*"K"' } 3))
    Check "new hotkey active" ((Text "HotkeyStatus") -match "active") (Text "HotkeyStatus")
    Click (Find "DefaultStyle_Cinematic")
    Check "default style saved" ([bool](Wait-Until { (Get-Content $settings -Raw) -match '"DefaultStyle":\s*"Cinematic"' } 3))
    Check "unsigned model reported" ((Text "ModelTitle") -match "not signed") (Text "ModelTitle")

    # Games: prepare and remove the fake game.
    Click (Find "Nav_Games")
    Check "fake game listed" ([bool](Find "Prepare_Fake Game"))
    Click (Find "Prepare_Fake Game")
    $yes = Find "QuestionYes" 8
    Check "warns that the model is not signed by NVIDIA" ([bool]$yes) (Text "QuestionNo")
    Click $yes
    Check "game prepared" ([bool](Find "Remove_Fake Game")) (Text "ToastText")
    $installed = @("winmm.dll", "OptiScaler.ini", "nvngx.dll_dlssnr.dll", "nvngx_dlssnr.dll", "neuralswitch.installed.json") |
        Where-Object { -not (Test-Path (Join-Path $game $_)) }
    Check "engine files in the game folder" ($installed.Count -eq 0) ("missing: " + ($installed -join ", "))
    Check "state shows Ready" ((Text "GameState_Fake Game") -eq "Ready") (Text "GameState_Fake Game")

    Click (Find "Remove_Fake Game")
    Check "game removed" ([bool](Find "Prepare_Fake Game"))
    $after = (Get-ChildItem $game | Sort-Object Name | ForEach-Object Name) -join ","
    Check "folder exactly as before" ($after -eq $gameFilesBefore) $after

    # About.
    Click (Find "Nav_About")
    Check "version shown" ((Text "VersionText") -match "^Version \d") (Text "VersionText")

    # Closing, with close-to-tray off (as set up above), ends the app.
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Check "window closes the app" ($proc.WaitForExit(10000))

    # With close-to-tray on, closing hides the window and NeuralSwitch keeps running; starting it a second
    # time brings that window back instead of opening another copy.
    $saved = Get-Content $settings -Raw | ConvertFrom-Json
    $saved.CloseToTray = $true
    $saved.TrayHintShown = $true
    $saved | ConvertTo-Json | Set-Content $settings -Encoding UTF8
    $proc = Start-Process $app -ArgumentList "--settings", "`"$settings`"" -PassThru
    $pidCondition = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
    $script:window = Wait-Until { $AE::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCondition) } 20
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    $hidden = Wait-Until { -not $AE::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCondition) } 5
    Check "closing keeps it running in the tray" ($hidden -and -not $proc.HasExited)

    $second = Start-Process $app -ArgumentList "--settings", "`"$settings`"" -PassThru
    Check "a second start exits" ($second.WaitForExit(10000))
    $back = Wait-Until { $AE::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCondition) } 5
    Check "and brings the first window back" ([bool]$back)
    $all = @(Get-Process NeuralSwitch -ErrorAction SilentlyContinue | Where-Object { $_.Id -eq $proc.Id -or $_.Id -eq $second.Id })
    Check "only one copy running" ($all.Count -eq 1) ("running: " + ($all.Id -join ", "))
}
catch {
    Check "unexpected error" $false $_.Exception.Message
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    if ($sim -and -not $sim.HasExited) { Stop-Process -Id $sim.Id -Force }
    Start-Sleep -Milliseconds 300
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($results | Where-Object { -not $_.Ok })
Write-Host ""
Write-Host ("{0} checks, {1} failed" -f $results.Count, $failed.Count)
if ($failed.Count -gt 0) { exit 1 }
