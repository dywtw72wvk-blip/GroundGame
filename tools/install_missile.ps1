param([string]$GameDataRoot = (Join-Path $env:APPDATA 'SpaceEngineers'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$blueprintDir = [IO.Path]::GetFullPath((Join-Path $GameDataRoot 'Blueprints\local\[GG] Topdown Missile'))
$scriptDir = [IO.Path]::GetFullPath((Join-Path $GameDataRoot 'IngameScripts\local\GroundGame Topdown'))
$backupRoot = Join-Path $repoRoot ('output\missile\backups\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-UTC')
if (Test-Path -LiteralPath $backupRoot) { throw 'Backup destination already exists' }
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
foreach ($entry in @(@($blueprintDir,'blueprint'),@($scriptDir,'script'))) {
    if (Test-Path -LiteralPath $entry[0]) { Copy-Item -LiteralPath $entry[0] -Destination (Join-Path $backupRoot $entry[1]) -Recurse }
    [IO.Directory]::CreateDirectory($entry[0]) | Out-Null
}
foreach ($pair in @(
    @((Join-Path $repoRoot 'blueprints\[GG] Topdown Missile\bp.sbc'),(Join-Path $blueprintDir 'bp.sbc')),
    @((Join-Path $repoRoot 'dist\TopdownMissile.cs'),(Join-Path $scriptDir 'Script.cs'))
)) {
    [IO.File]::Copy($pair[0],$pair[1],$true)
    if ((Get-FileHash -LiteralPath $pair[0]).Hash -ne (Get-FileHash -LiteralPath $pair[1]).Hash) { throw 'Installed file mismatch' }
}
$cachePath = [IO.Path]::GetFullPath((Join-Path $blueprintDir 'bp.sbcB5'))
if ([IO.Path]::GetDirectoryName($cachePath) -ne $blueprintDir) { throw 'Cache path escaped blueprint directory' }
if (Test-Path -LiteralPath $cachePath) {
    $savedCache = Join-Path $backupRoot 'blueprint\bp.sbcB5'
    if (!(Test-Path -LiteralPath $savedCache) -or (Get-FileHash -LiteralPath $cachePath).Hash -ne (Get-FileHash -LiteralPath $savedCache).Hash) { throw 'Binary cache changed during install; preserved it for inspection' }
    Remove-Item -LiteralPath $cachePath
}
Write-Output "Installed blueprint: $blueprintDir"
Write-Output "Installed script: $scriptDir"
Write-Output "Previous copies preserved: $backupRoot"
