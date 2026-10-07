#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${1:-${BOOBOOP_GAME_DIR:-}}"
[[ -n "$game_dir" ]] || { echo 'Pass your game directory as the first argument, or set BOOBOOP_GAME_DIR.' >&2; exit 2; }
dotnet build "$repo_root/SecretFlasherManaka.ForEveryThing.csproj" -c "${2:-Release}" "-p:GameDir=$game_dir"
