[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$LogPath = 'TestResults/validation.log'
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
$absoluteLogPath = if ([IO.Path]::IsPathRooted($LogPath)) {
    [IO.Path]::GetFullPath($LogPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $LogPath))
}
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteLogPath) -Force | Out-Null
if (Test-Path -LiteralPath $absoluteLogPath) {
    Remove-Item -LiteralPath $absoluteLogPath -Force
}

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $projectRoot,
    '-executeMethod', 'Game.Editor.ProjectValidationCommand.Run',
    '-logFile', $absoluteLogPath
)
$process = Start-Process `
    -FilePath $unityExecutable `
    -ArgumentList $arguments `
    -Wait `
    -PassThru `
    -WindowStyle Hidden
Write-Host "Validation Unity exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}

if (-not (Test-Path -LiteralPath $absoluteLogPath -PathType Leaf)) {
    [Console]::Error.WriteLine(
        "Unity exited successfully but did not create the validation log: $absoluteLogPath")
    exit 4
}

if (-not (Select-String -LiteralPath $absoluteLogPath -SimpleMatch '[Project Validation] PASS' -Quiet)) {
    [Console]::Error.WriteLine(
        "Validation log does not contain the required PASS marker: $absoluteLogPath")
    exit 5
}

Write-Host "Validation result: PASS"
exit 0
