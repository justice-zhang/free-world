[CmdletBinding()]
param(
    [Alias('PlayerPath')]
    [string]$Executable = 'Builds/WindowsRelease/AzureSword.exe',
    [string]$ProjectPath = '',
    [string]$LogPath = 'TestResults/M10Final/release-player.log',
    [Alias('OutputPath')]
    [string]$ResultPath = 'TestResults/M10Final/release-player.json'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$absoluteExecutable = if ([IO.Path]::IsPathRooted($Executable)) {
    [IO.Path]::GetFullPath($Executable)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $Executable))
}
$absoluteLog = if ([IO.Path]::IsPathRooted($LogPath)) {
    [IO.Path]::GetFullPath($LogPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $LogPath))
}
$absoluteResult = if ([IO.Path]::IsPathRooted($ResultPath)) {
    [IO.Path]::GetFullPath($ResultPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $ResultPath))
}
if (-not (Test-Path -LiteralPath $absoluteExecutable -PathType Leaf)) {
    [Console]::Error.WriteLine("Release executable does not exist: $absoluteExecutable")
    exit 2
}
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteLog) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteResult) -Force | Out-Null
foreach ($path in @($absoluteLog, $absoluteResult)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

$previousResult = $env:M10_SMOKE_RESULT
try {
    $env:M10_SMOKE_RESULT = $absoluteResult
    $process = Start-Process -FilePath $absoluteExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-logFile', $absoluteLog) `
        -PassThru -WindowStyle Hidden
    [void]$process.WaitForExit()
    $process.Refresh()
} finally {
    $env:M10_SMOKE_RESULT = $previousResult
}
Write-Host "Release player exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
if (-not (Test-Path -LiteralPath $absoluteResult -PathType Leaf)) {
    [Console]::Error.WriteLine("Release smoke result is missing: $absoluteResult")
    exit 4
}
try {
    $result = Get-Content -LiteralPath $absoluteResult -Raw | ConvertFrom-Json
} catch {
    [Console]::Error.WriteLine("Release smoke result is invalid: $($_.Exception.Message)")
    exit 5
}
if ($result.status -ne 'PASS' -or [int]$result.ticks -ne 60 -or
    [int]$result.actors -ne 4 -or [int]$result.invalidHandleAccesses -ne 0) {
    [Console]::Error.WriteLine('Release player did not complete the deterministic smoke run.')
    exit 5
}
if (-not (Select-String -LiteralPath $absoluteLog -SimpleMatch '[M10 Release Smoke] PASS' -Quiet)) {
    [Console]::Error.WriteLine("Release player log has no PASS marker: $absoluteLog")
    exit 5
}
Write-Host "Release player smoke result: PASS ($absoluteResult)"
exit 0
