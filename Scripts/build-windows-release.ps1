[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$OutputPath = 'Builds/WindowsRelease/AzureSword.exe',
    [string]$LogPath = 'TestResults/build-windows-release.log',
    [string]$EvidenceRoot = 'TestResults/M10Final'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$unityExecutable = $env:UNITY_PATH
if ([string]::IsNullOrWhiteSpace($unityExecutable) -or
    -not (Test-Path -LiteralPath $unityExecutable -PathType Leaf)) {
    [Console]::Error.WriteLine('UNITY_PATH must point to the locked Unity executable.')
    exit 2
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$absoluteOutput = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
}
$absoluteLog = if ([IO.Path]::IsPathRooted($LogPath)) {
    [IO.Path]::GetFullPath($LogPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $LogPath))
}
$absoluteEvidence = if ([IO.Path]::IsPathRooted($EvidenceRoot)) {
    [IO.Path]::GetFullPath($EvidenceRoot)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $EvidenceRoot))
}
$outputDirectory = Split-Path -Parent $absoluteOutput
$manifestPath = Join-Path $outputDirectory 'BuildManifest.json'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteLog) -Force | Out-Null
foreach ($path in @($absoluteOutput, $absoluteLog, $manifestPath)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

$previousOutput = $env:BUILD_OUTPUT
$previousEvidence = $env:M10_EVIDENCE_ROOT
try {
    $env:BUILD_OUTPUT = $absoluteOutput
    $env:M10_EVIDENCE_ROOT = $absoluteEvidence
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-executeMethod', 'Game.Editor.WindowsReleaseBuild.BuildFromCommandLine',
        '-logFile', $absoluteLog
    )
    $process = Start-Process -FilePath $unityExecutable -ArgumentList $arguments `
        -PassThru -WindowStyle Hidden
    [void]$process.WaitForExit()
    $process.Refresh()
} finally {
    $env:BUILD_OUTPUT = $previousOutput
    $env:M10_EVIDENCE_ROOT = $previousEvidence
}

Write-Host "Windows Release Build Unity exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
if (-not (Test-Path -LiteralPath $absoluteOutput -PathType Leaf)) {
    [Console]::Error.WriteLine("Release executable is missing: $absoluteOutput")
    exit 5
}
if (-not (Test-Path -LiteralPath $absoluteLog -PathType Leaf) -or
    -not (Select-String -LiteralPath $absoluteLog -SimpleMatch '[M10 Release Build] PASS' -Quiet)) {
    [Console]::Error.WriteLine("Release build log has no PASS marker: $absoluteLog")
    exit 6
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    [Console]::Error.WriteLine("Release Build Manifest is missing: $manifestPath")
    exit 6
}
try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    [Console]::Error.WriteLine("Release Build Manifest is invalid: $($_.Exception.Message)")
    exit 6
}
if ($manifest.result -ne 'Succeeded' -or
    $manifest.buildTarget -ne 'StandaloneWindows64' -or
    [bool]$manifest.development -or
    [int]$manifest.placeholderCount -ne 0 -or
    $manifest.buildConfiguration -ne 'WindowsReleaseVerification') {
    [Console]::Error.WriteLine('Build Manifest does not describe a placeholder-free Release build.')
    exit 6
}
Write-Host "Release build output: $absoluteOutput"
Write-Host "Release Build Manifest: $manifestPath"
exit 0
