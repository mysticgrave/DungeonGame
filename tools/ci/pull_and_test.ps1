param(
  [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\").Path,
  [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
)

$ErrorActionPreference = 'Stop'

Set-Location $RepoRoot

Write-Host "Repo: $RepoRoot"

git fetch --all
# Try fast-forward only to avoid accidental merge commits
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
Write-Host "Branch: $branch"

git pull --ff-only

& (Join-Path $RepoRoot "tools\ci\run_unity_tests.ps1") -UnityExe $UnityExe -ProjectPath $RepoRoot
