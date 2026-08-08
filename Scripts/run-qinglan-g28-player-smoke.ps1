[CmdletBinding()]
param(
    [string]$Executable = 'Builds/WindowsDevelopment/AzureSword.exe',
    [string]$ProjectPath = '',
    [string]$LogPath = 'TestResults/QinglanDemo/G2.8/player-smoke.log',
    [string]$ResultPath = 'TestResults/QinglanDemo/G2.8/player-smoke.json',
    [string]$SavePath = 'TestResults/QinglanDemo/G2.8/player-save'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
function Resolve-ProjectPath([string]$Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $projectRoot $Value))
}
$absoluteExecutable = Resolve-ProjectPath $Executable
$absoluteLog = Resolve-ProjectPath $LogPath
$absoluteResult = Resolve-ProjectPath $ResultPath
$absoluteSave = Resolve-ProjectPath $SavePath
if (-not (Test-Path -LiteralPath $absoluteExecutable -PathType Leaf)) {
    [Console]::Error.WriteLine("Development executable does not exist: $absoluteExecutable")
    exit 2
}
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteLog) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteResult) -Force | Out-Null
New-Item -ItemType Directory -Path $absoluteSave -Force | Out-Null
foreach ($path in @($absoluteLog, $absoluteResult)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

$previousResult = $env:QINGLAN_G28_PLAYER_RESULT
$previousSave = $env:AZURESWORD_SAVE_ROOT
try {
    $env:QINGLAN_G28_PLAYER_RESULT = $absoluteResult
    $env:AZURESWORD_SAVE_ROOT = $absoluteSave
    $process = Start-Process -FilePath $absoluteExecutable `
        -ArgumentList @('-batchmode', '-nographics', '-qinglanG28Smoke', '-logFile', $absoluteLog) `
        -PassThru -WindowStyle Hidden
    [void]$process.WaitForExit()
    $process.Refresh()
} finally {
    $env:QINGLAN_G28_PLAYER_RESULT = $previousResult
    $env:AZURESWORD_SAVE_ROOT = $previousSave
}
Write-Host "Development player exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
if (-not (Test-Path -LiteralPath $absoluteResult -PathType Leaf)) {
    [Console]::Error.WriteLine("G2.8 Player smoke result is missing: $absoluteResult")
    exit 4
}
try {
    $result = Get-Content -LiteralPath $absoluteResult -Raw | ConvertFrom-Json
} catch {
    [Console]::Error.WriteLine("G2.8 Player smoke result is invalid: $($_.Exception.Message)")
    exit 5
}
if ($result.status -ne 'PASS' -or -not $result.titleVisited -or
    -not $result.activeRunVisited -or -not $result.resultVisited -or
    -not $result.saveCommitted -or -not $result.hubVisited -or
    -not $result.restartVisited -or [int]$result.activeViewsAfterHub -ne 0 -or
    [int]$result.inputOwnerCount -ne 1) {
    [Console]::Error.WriteLine('G2.8 Development Player did not complete the real UI lifecycle smoke.')
    exit 5
}
if (-not (Select-String -LiteralPath $absoluteLog -SimpleMatch '[Qinglan G2.8 Player Smoke] PASS' -Quiet)) {
    [Console]::Error.WriteLine("G2.8 Player log has no PASS marker: $absoluteLog")
    exit 5
}
Write-Host "Qinglan G2.8 Player smoke result: PASS ($absoluteResult)"
exit 0
