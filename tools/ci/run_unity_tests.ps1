param(
  [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe",
  [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..\").Path,
  [string]$ResultsDir = "",
  [switch]$RunEditMode = $true,
  [switch]$RunPlayMode = $false
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ResultsDir)) {
  $ResultsDir = Join-Path $ProjectPath "Logs\automation\$(Get-Date -Format 'yyyyMMdd-HHmmss')"
}

New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

$logPath = Join-Path $ResultsDir "unity.log"

Write-Host "Unity: $UnityExe"
Write-Host "Project: $ProjectPath"
Write-Host "Results: $ResultsDir"

if (!(Test-Path $UnityExe)) {
  throw "Unity.exe not found at: $UnityExe"
}

$baseArgs = @(
  '-batchmode',
  '-nographics',
  '-quit',
  '-projectPath', "$ProjectPath",
  '-logFile', "$logPath"
)

function Invoke-Unity {
  param([string[]]$Args)

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $UnityExe
  $psi.Arguments = ($Args -join ' ')
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true

  $p = New-Object System.Diagnostics.Process
  $p.StartInfo = $psi

  Write-Host "Running: $UnityExe $($psi.Arguments)"

  $null = $p.Start()
  $stdout = $p.StandardOutput.ReadToEnd()
  $stderr = $p.StandardError.ReadToEnd()
  $p.WaitForExit()

  if ($stdout) { $stdout | Out-File -FilePath (Join-Path $ResultsDir 'stdout.txt') -Encoding utf8 }
  if ($stderr) { $stderr | Out-File -FilePath (Join-Path $ResultsDir 'stderr.txt') -Encoding utf8 }

  Write-Host "ExitCode: $($p.ExitCode)"
  return $p.ExitCode
}

$exitCodes = @()

if ($RunEditMode) {
  $editXml = Join-Path $ResultsDir 'editmode-results.xml'
  $args = $baseArgs + @(
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testResults', "$editXml"
  )
  $exitCodes += (Invoke-Unity -Args $args)
}

if ($RunPlayMode) {
  $playXml = Join-Path $ResultsDir 'playmode-results.xml'
  $args = $baseArgs + @(
    '-runTests',
    '-testPlatform', 'PlayMode',
    '-testResults', "$playXml"
  )
  $exitCodes += (Invoke-Unity -Args $args)
}

# Unity returns non-zero for failures; propagate failure if any non-zero
if ($exitCodes | Where-Object { $_ -ne 0 }) {
  Write-Error "Unity tests failed. See: $ResultsDir"
  exit 1
}

Write-Host "Unity tests completed successfully. Logs in: $ResultsDir"
exit 0
