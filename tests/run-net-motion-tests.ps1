param(
    [int[]]$Scales = @(1, 3),
    [int[]]$Displays = @(),
    [string]$OutputDirectory
)
# Motion suites against the .NET 10 program (src\DesktopPet.Net.csproj), the build that is released.
# Needs only the .NET 10 SDK (no Visual Studio, no .NET Framework targeting pack). x64 only, like the release.
# Close a running DesktopPet first: the suites show real pets on screen.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot 'MotionTests.Net.csproj'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) ('DesktopPet-net-motion-tests-' + [guid]::NewGuid().ToString('N')) }
$runtime = $OutputDirectory
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
Add-Type -AssemblyName System.Windows.Forms
if ($Displays.Count -eq 0) { $Displays = @(0..([Windows.Forms.Screen]::AllScreens.Length - 1)) }
Write-Output "Isolated test output: $runtime"

$suites = 'MotionTests', 'DisplayTopologyTests', 'MotionAuditTests', 'RuntimeMotionTests', 'TransitionRuntimeTests', 'ContinuousTransitionTests'
foreach ($suite in $suites) {
    & dotnet build $project -c Release --nologo -v q "-p:TestName=$suite" -o $runtime
    if ($LASTEXITCODE -ne 0) { throw "$suite build failed." }
}

function Invoke-Suite([string]$name, [string[]]$arguments) {
    Push-Location $runtime
    try { & (Join-Path $runtime "$name.exe") @arguments } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "$name failed ($($arguments -join ' '))." }
}

Invoke-Suite 'MotionTests' @()
Invoke-Suite 'MotionAuditTests' @()
Invoke-Suite 'DisplayTopologyTests' @()
foreach ($scale in $Scales) {
    foreach ($display in $Displays) {
        Invoke-Suite 'RuntimeMotionTests' @("$scale", "$display")
        Invoke-Suite 'TransitionRuntimeTests' @("$scale", "$display")
        Invoke-Suite 'ContinuousTransitionTests' @("$scale", "$display")
    }
}
Write-Output 'All .NET 10 motion suites passed.'
