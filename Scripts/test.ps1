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
    foreach ($generatedPath in @($resultPath, $logPath)) {
        if (Test-Path -LiteralPath $generatedPath) {
            Remove-Item -LiteralPath $generatedPath -Force
        }
    }

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
        -PassThru `
        -WindowStyle Hidden
    [void]$process.WaitForExit()
    $process.Refresh()
    Write-Host "$TestPlatform Unity exit code: $($process.ExitCode)"
    if ($process.ExitCode -ne 0) {
        return $process.ExitCode
    }

    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        [Console]::Error.WriteLine(
            "$TestPlatform Unity exited successfully but did not create test results: $resultPath")
        return 4
    }

    try {
        [xml]$resultDocument = Get-Content -LiteralPath $resultPath -Raw
        $testRun = $resultDocument.'test-run'
        if ($null -eq $testRun) {
            throw 'The XML does not contain a test-run root element.'
        }

        $total = [int]$testRun.total
        $passed = [int]$testRun.passed
        $failed = [int]$testRun.failed
        $skipped = [int]$testRun.skipped
    } catch {
        [Console]::Error.WriteLine(
            "$TestPlatform produced invalid test results at ${resultPath}: $($_.Exception.Message)")
        return 4
    }

    Write-Host (
        "$TestPlatform results: total=$total passed=$passed failed=$failed skipped=$skipped " +
        "result=$($testRun.result)")
    if ($testRun.result -ne 'Passed' -or $failed -ne 0) {
        [Console]::Error.WriteLine("$TestPlatform test results are not passing.")
        return 5
    }

    return 0
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
