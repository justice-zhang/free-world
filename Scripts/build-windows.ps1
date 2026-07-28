[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$OutputPath = 'Builds/WindowsDevelopment/AzureSword.exe',
    [string]$LogPath = 'TestResults/build-windows.log',
    [string]$EvidenceRoot = 'TestResults/M10Final'
)

$ErrorActionPreference = 'Stop'
$unityExecutable = $env:UNITY_PATH

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}

if ([string]::IsNullOrWhiteSpace($unityExecutable)) {
    [Console]::Error.WriteLine('UNITY_PATH is not set.')
    exit 2
}

if (-not (Test-Path -LiteralPath $unityExecutable -PathType Leaf)) {
    [Console]::Error.WriteLine("UNITY_PATH does not point to a file: $unityExecutable")
    exit 3
}

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    [Console]::Error.WriteLine("Project path does not exist: $ProjectPath")
    exit 2
}

$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$absoluteOutputPath = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
}
$absoluteLogPath = if ([IO.Path]::IsPathRooted($LogPath)) {
    [IO.Path]::GetFullPath($LogPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $LogPath))
}
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteLogPath) -Force | Out-Null
$absoluteEvidenceRoot = if ([IO.Path]::IsPathRooted($EvidenceRoot)) {
    [IO.Path]::GetFullPath($EvidenceRoot)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $EvidenceRoot))
}
$outputDirectory = Split-Path -Parent $absoluteOutputPath
$manifestPath = Join-Path $outputDirectory 'BuildManifest.json'

foreach ($generatedPath in @($absoluteOutputPath, $absoluteLogPath, $manifestPath)) {
    if (Test-Path -LiteralPath $generatedPath) {
        Remove-Item -LiteralPath $generatedPath -Force
    }
}

$previousBuildOutput = $env:BUILD_OUTPUT
$previousEvidenceRoot = $env:M10_EVIDENCE_ROOT
try {
    $env:BUILD_OUTPUT = $absoluteOutputPath
    $env:M10_EVIDENCE_ROOT = $absoluteEvidenceRoot
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-executeMethod', 'Game.Editor.WindowsDevelopmentBuild.BuildFromCommandLine',
        '-logFile', $absoluteLogPath
    )
    $process = Start-Process `
        -FilePath $unityExecutable `
        -ArgumentList $arguments `
        -PassThru `
        -WindowStyle Hidden
    # Start-Process -Wait follows the full descendant process tree on Windows.
    # Unity build helpers can outlive the Editor even after a successful build,
    # so wait for the Editor process itself and then read its real exit code.
    [void]$process.WaitForExit()
    $process.Refresh()
} finally {
    $env:BUILD_OUTPUT = $previousBuildOutput
    $env:M10_EVIDENCE_ROOT = $previousEvidenceRoot
}

Write-Host "Windows Development Build Unity exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}

if (-not (Test-Path -LiteralPath $absoluteOutputPath -PathType Leaf)) {
    [Console]::Error.WriteLine("Unity reported success but the executable is missing: $absoluteOutputPath")
    exit 5
}

if (-not (Test-Path -LiteralPath $absoluteLogPath -PathType Leaf) -or
    -not (Select-String -LiteralPath $absoluteLogPath -SimpleMatch '[M0 Build] PASS' -Quiet)) {
    [Console]::Error.WriteLine(
        "Build log does not contain the required PASS marker: $absoluteLogPath")
    exit 6
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    [Console]::Error.WriteLine("Build manifest is missing: $manifestPath")
    exit 6
}

try {
    $buildManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifestExecutable = [IO.Path]::GetFullPath([string]$buildManifest.executable)
} catch {
    [Console]::Error.WriteLine(
        "Build manifest is invalid at ${manifestPath}: $($_.Exception.Message)")
    exit 6
}

if ($buildManifest.result -ne 'Succeeded' -or
    $buildManifest.buildTarget -ne 'StandaloneWindows64' -or
    -not [bool]$buildManifest.development -or
    -not $manifestExecutable.Equals($absoluteOutputPath, [StringComparison]::OrdinalIgnoreCase)) {
    [Console]::Error.WriteLine("Build manifest does not describe the requested Development Build.")
    exit 6
}

Write-Host "Build output: $absoluteOutputPath"
Write-Host "Build manifest: $manifestPath"
exit 0
