[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$OutputPath = 'TestResults/M10Performance/performance.json',
    [string]$LogPath = 'TestResults/M10Performance/performance.log',
    [int]$TickCount = 54000,
    [int]$EnemyCount = 1500,
    [int]$ProjectileCount = 3000,
    [int]$PickupCount = 5000,
    [int]$VfxCount = 200,
    [int]$WarmupTicks = 300,
    [UInt64]$Seed = 5562305753520559450,
    [string]$EnemyId = 'test.enemy.ranged'
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
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteOutput) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteLog) -Force | Out-Null
foreach ($path in @($absoluteOutput, $absoluteLog)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

$previous = @{
    output = $env:M10_PERFORMANCE_OUTPUT
    ticks = $env:M10_TICK_COUNT
    enemies = $env:M10_ENEMY_COUNT
    projectiles = $env:M10_PROJECTILE_COUNT
    pickups = $env:M10_PICKUP_COUNT
    vfx = $env:M10_VFX_COUNT
    warmup = $env:M10_WARMUP_TICKS
    seed = $env:M10_SEED
    enemyId = $env:M10_ENEMY_ID
}
try {
    $env:M10_PERFORMANCE_OUTPUT = $absoluteOutput
    $env:M10_TICK_COUNT = [string]$TickCount
    $env:M10_ENEMY_COUNT = [string]$EnemyCount
    $env:M10_PROJECTILE_COUNT = [string]$ProjectileCount
    $env:M10_PICKUP_COUNT = [string]$PickupCount
    $env:M10_VFX_COUNT = [string]$VfxCount
    $env:M10_WARMUP_TICKS = [string]$WarmupTicks
    $env:M10_SEED = [string]$Seed
    $env:M10_ENEMY_ID = $EnemyId
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-executeMethod', 'Game.Editor.M10PerformanceCommand.Run',
        '-logFile', $absoluteLog
    )
    $process = Start-Process -FilePath $unityExecutable -ArgumentList $arguments `
        -PassThru -WindowStyle Hidden
    [void]$process.WaitForExit()
    $process.Refresh()
} finally {
    $env:M10_PERFORMANCE_OUTPUT = $previous.output
    $env:M10_TICK_COUNT = $previous.ticks
    $env:M10_ENEMY_COUNT = $previous.enemies
    $env:M10_PROJECTILE_COUNT = $previous.projectiles
    $env:M10_PICKUP_COUNT = $previous.pickups
    $env:M10_VFX_COUNT = $previous.vfx
    $env:M10_WARMUP_TICKS = $previous.warmup
    $env:M10_SEED = $previous.seed
    $env:M10_ENEMY_ID = $previous.enemyId
}

Write-Host "M10 Performance Unity exit code: $($process.ExitCode)"
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
if (-not (Test-Path -LiteralPath $absoluteOutput -PathType Leaf)) {
    [Console]::Error.WriteLine("Performance JSON is missing: $absoluteOutput")
    exit 4
}
if (-not (Select-String -LiteralPath $absoluteLog -SimpleMatch '[M10 Performance] PASS' -Quiet)) {
    [Console]::Error.WriteLine("Performance log has no PASS marker: $absoluteLog")
    exit 5
}
try {
    $report = Get-Content -LiteralPath $absoluteOutput -Raw | ConvertFrom-Json
} catch {
    [Console]::Error.WriteLine("Performance JSON is invalid: $($_.Exception.Message)")
    exit 5
}
if ($report.status -ne 'PASS' -or
    [int]$report.configuration.tickCount -ne $TickCount -or
    [int]$report.configuration.enemies -ne $EnemyCount -or
    [int]$report.configuration.projectiles -ne $ProjectileCount -or
    [int]$report.configuration.pickups -ne $PickupCount -or
    [int]$report.configuration.vfxRequests -ne $VfxCount) {
    [Console]::Error.WriteLine('Performance JSON does not describe a passing requested run.')
    exit 5
}
Write-Host "M10 Performance result: PASS ($absoluteOutput)"
exit 0
