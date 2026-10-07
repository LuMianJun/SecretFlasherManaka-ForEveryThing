param([string]$GameDir = $env:BOOBOOP_GAME_DIR, [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Set -GameDir or BOOBOOP_GAME_DIR to your game installation.' }
& dotnet build (Join-Path $PSScriptRoot 'SecretFlasherManaka.ForEveryThing.csproj') -c $Configuration "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Game plugin build failed.' }
