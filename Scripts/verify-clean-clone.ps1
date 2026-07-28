[CmdletBinding()]
param(
    [string]$SourceRepository = '',
    [string]$Branch = '',
    [string]$WorkRoot = '',
    [string]$EvidenceOutput = '',
    [int]$PerformanceTickCount = 54000,
    [switch]$KeepClone
)

$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceRepository)) { $SourceRepository = $sourceRoot }
if ([string]::IsNullOrWhiteSpace($Branch)) {
    $Branch = (& git -C $sourceRoot rev-parse --abbrev-ref HEAD).Trim()
}
if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path $env:TEMP ('free-world-m10-clean-' + [Guid]::NewGuid().ToString('N'))
}
if ([string]::IsNullOrWhiteSpace($EvidenceOutput)) {
    $EvidenceOutput = Join-Path $sourceRoot 'TestResults/M10CleanCloneEvidence'
}
if ([string]::IsNullOrWhiteSpace($env:UNITY_PATH) -or
    -not (Test-Path -LiteralPath $env:UNITY_PATH -PathType Leaf)) {
    throw 'UNITY_PATH must point to the locked Unity executable.'
}
$resolvedWorkRoot = [IO.Path]::GetFullPath($WorkRoot)
if (Test-Path -LiteralPath $resolvedWorkRoot) {
    throw "Clean-clone target already exists: $resolvedWorkRoot"
}
$cloneRoot = Join-Path $resolvedWorkRoot 'free-world'
New-Item -ItemType Directory -Path $resolvedWorkRoot | Out-Null

try {
    & git clone --no-local --single-branch --branch $Branch $SourceRepository $cloneRoot
    if ($LASTEXITCODE -ne 0) { throw "git clone failed with exit code $LASTEXITCODE" }
    $lockedLine = Get-Content -LiteralPath (Join-Path $cloneRoot 'ProjectSettings/ProjectVersion.txt') `
        | Select-String -Pattern '^m_EditorVersion:' | Select-Object -First 1
    $lockedVersion = ($lockedLine.Line -split ':', 2)[1].Trim()
    $editorDirectory = Split-Path -Parent $env:UNITY_PATH
    $installedVersion = Split-Path -Leaf (Split-Path -Parent $editorDirectory)
    if ($installedVersion -ne $lockedVersion) {
        throw "Unity mismatch: project=$lockedVersion installed=$installedVersion"
    }

    $evidence = Join-Path $cloneRoot 'TestResults/M10CleanClone'
    & (Join-Path $cloneRoot 'Scripts/test.ps1') -Platform EditMode `
        -ProjectPath $cloneRoot -ResultsDirectory $evidence
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone EditMode failed.' }
    & (Join-Path $cloneRoot 'Scripts/test.ps1') -Platform PlayMode `
        -ProjectPath $cloneRoot -ResultsDirectory $evidence
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone PlayMode failed.' }
    & (Join-Path $cloneRoot 'Scripts/validate.ps1') -ProjectPath $cloneRoot `
        -LogPath (Join-Path $evidence 'validation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone validation failed.' }
    & (Join-Path $cloneRoot 'Scripts/run-performance.ps1') -ProjectPath $cloneRoot `
        -OutputPath (Join-Path $evidence 'performance.json') `
        -LogPath (Join-Path $evidence 'performance.log') `
        -TickCount $PerformanceTickCount
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone performance gate failed.' }
    & (Join-Path $cloneRoot 'Scripts/build-windows.ps1') -ProjectPath $cloneRoot `
        -OutputPath 'Builds/WindowsDevelopment/AzureSword.exe' `
        -LogPath (Join-Path $evidence 'build-development.log') `
        -EvidenceRoot $evidence
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone Development build failed.' }
    & (Join-Path $cloneRoot 'Scripts/build-windows-release.ps1') -ProjectPath $cloneRoot `
        -OutputPath 'Builds/WindowsRelease/AzureSword.exe' `
        -LogPath (Join-Path $evidence 'build-release.log') `
        -EvidenceRoot $evidence
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone Release build failed.' }
    & (Join-Path $cloneRoot 'Scripts/run-player-smoke.ps1') -ProjectPath $cloneRoot `
        -Executable 'Builds/WindowsRelease/AzureSword.exe' `
        -LogPath (Join-Path $evidence 'release-player.log') `
        -ResultPath (Join-Path $evidence 'release-player.json')
    if ($LASTEXITCODE -ne 0) { throw 'Clean-clone Release player smoke failed.' }

    New-Item -ItemType Directory -Path $EvidenceOutput -Force | Out-Null
    Copy-Item -Path (Join-Path $evidence '*') -Destination $EvidenceOutput -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $cloneRoot 'Builds/WindowsDevelopment/BuildManifest.json') `
        -Destination (Join-Path $EvidenceOutput 'DevelopmentBuildManifest.json') -Force
    Copy-Item -LiteralPath (Join-Path $cloneRoot 'Builds/WindowsRelease/BuildManifest.json') `
        -Destination (Join-Path $EvidenceOutput 'ReleaseBuildManifest.json') -Force
    $dirtyTracked = & git -C $cloneRoot diff --name-only
    $dirtyStaged = & git -C $cloneRoot diff --cached --name-only
    $untracked = & git -C $cloneRoot ls-files --others --exclude-standard
    if ($dirtyTracked -or $dirtyStaged -or $untracked) {
        throw 'Clean-clone gates left source-controlled or untracked project files behind.'
    }
    Write-Host "M10 clean clone: PASS ($cloneRoot)"
} finally {
    if ($KeepClone) {
        Write-Host "Clean clone retained: $cloneRoot"
    } else {
        Write-Host "Clean clone retained for audit; remove explicitly after reviewing: $cloneRoot"
    }
}
