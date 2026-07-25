[CmdletBinding()]
param(
    [ValidateSet('All', 'EditMode', 'PlayMode')]
    [string]$Platform = 'All',
    [string]$ProjectPath = '',
    [string]$ResultsDirectory = 'TestResults'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}

function Exit-WithConfigurationError {
    param(
        [string]$Message,
        [int]$Code
    )

    [Console]::Error.WriteLine($Message)
    exit $Code
}

$unityExecutable = $env:UNITY_PATH
if ([string]::IsNullOrWhiteSpace($unityExecutable)) {
    Exit-WithConfigurationError 'UNITY_PATH is not set.' 2
}

if (-not (Test-Path -LiteralPath $unityExecutable -PathType Leaf)) {
    Exit-WithConfigurationError "UNITY_PATH does not point to a file: $unityExecutable" 3
}

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    Exit-WithConfigurationError "Project path does not exist: $ProjectPath" 2
}

$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$resultsRoot = if ([IO.Path]::IsPathRooted($ResultsDirectory)) {
    [IO.Path]::GetFullPath($ResultsDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $ResultsDirectory))
}
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

function Invoke-UnityTests {
    param([string]$TestPlatform)

    $resultPath = Join-Path $resultsRoot "$($TestPlatform.ToLowerInvariant()).xml"
    $logPath = Join-Path $resultsRoot "$($TestPlatform.ToLowerInvariant()).log"
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-runTests',
        '-testPlatform', $TestPlatform,
        '-testResults', $resultPath,
        '-logFile', $logPath
    )

    $process = Start-Process `
        -FilePath $unityExecutable `
        -ArgumentList $arguments `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Write-Host "$TestPlatform Unity exit code: $($process.ExitCode)"
    return $process.ExitCode
}

$platforms = if ($Platform -eq 'All') {
    @('EditMode', 'PlayMode')
} else {
    @($Platform)
}

foreach ($testPlatform in $platforms) {
    $exitCode = Invoke-UnityTests $testPlatform
    if ($exitCode -ne 0) {
        exit $exitCode
    }
}

exit 0
