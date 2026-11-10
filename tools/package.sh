#!/usr/bin/env bash
# Build both mods and assemble a distributable layout under dist/.
set -euo pipefail

EXPORT_PATH="${EXPORT_PATH:-$HOME/.dotnet9}"
export PATH="$EXPORT_PATH:$PATH"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_MODS="D:/SteamLibrary/steamapps/common/Slay the Spire 2/mods"
OUT="$ROOT/dist"

echo "== build =="
(cd "$ROOT" && dotnet build -c Release)
(cd "$ROOT/../CombatSolverCompat" && dotnet build -c Release)

echo "== assemble =="
rm -rf "$OUT"
mkdir -p "$OUT/WatcherAdapter" "$OUT/CombatSolverCompat"
cp "$ROOT/bin/Release/WatcherAdapter.dll" "$OUT/WatcherAdapter/"
cp "$GAME_MODS/WatcherAdapter/WatcherAdapter.json" "$OUT/WatcherAdapter/"
cp "$ROOT/../CombatSolverCompat/bin/Release/CombatSolverCompat.dll" "$OUT/CombatSolverCompat/"
cp "$GAME_MODS/CombatSolverCompat/CombatSolverCompat.json" "$OUT/CombatSolverCompat/"
cp "$ROOT/README.md" "$OUT/WatcherAdapter/README.md"
cp "$ROOT/COVERAGE.md" "$OUT/WatcherAdapter/COVERAGE.md"

echo "== zip =="
cd "$OUT"
rm -f WatcherAdapter.zip CombatSolverCompat.zip
# zip(1) is not available in plain Git-Bash on Windows; use PowerShell instead.
powershell.exe -NoProfile -Command "Compress-Archive -Path 'WatcherAdapter' -DestinationPath 'WatcherAdapter.zip' -Force"
powershell.exe -NoProfile -Command "Compress-Archive -Path 'CombatSolverCompat' -DestinationPath 'CombatSolverCompat.zip' -Force"

echo "== result =="
ls -la "$OUT"
