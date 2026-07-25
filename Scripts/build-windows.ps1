[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$OutputPath = 'Builds/WindowsDevelopment/AzureSword.exe',
    [string]$LogPath = 'TestResults/build-windows.log'
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

$previousBuildOutput = $env:BUILD_OUTPUT
try {
    $env:BUILD_OUTPUT = $absoluteOutputPath
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
        -Wait `
        -PassThru `
        -WindowStyle Hidden
} finally {
    $env:BUILD_OUTPUT = $previousBuildOutput
}

Write-Host "Windows Development Build Unity exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}

if (-not (Test-Path -LiteralPath $absoluteOutputPath -PathType Leaf)) {
    [Console]::Error.WriteLine("Unity reported success but the executable is missing: $absoluteOutputPath")
    exit 5
}

Write-Host "Build output: $absoluteOutputPath"
exit 0
