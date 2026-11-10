#!/usr/bin/env bash
# Build both mods and assemble a distributable layout under dist/.
set -euo pipefail

export PATH="${EXPORT_PATH:-$HOME/.dotnet9}:$PATH"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/dist"

echo "== build =="
(cd "$ROOT" && dotnet build -c Release)
(cd "$ROOT/CombatSolverCompat" && dotnet build -c Release)

echo "== assemble =="
rm -rf "$OUT"
mkdir -p "$OUT/WatcherAdapter" "$OUT/CombatSolverCompat"

cp "$ROOT/bin/Release/WatcherAdapter.dll" "$OUT/WatcherAdapter/"
cp "$ROOT/mods/WatcherAdapter/WatcherAdapter.json" "$OUT/WatcherAdapter/"
cp "$ROOT/README.md" "$OUT/WatcherAdapter/README.md"
cp "$ROOT/COVERAGE.md" "$OUT/WatcherAdapter/COVERAGE.md"

cp "$ROOT/CombatSolverCompat/bin/Release/CombatSolverCompat.dll" "$OUT/CombatSolverCompat/"
cp "$ROOT/mods/CombatSolverCompat/CombatSolverCompat.json" "$OUT/CombatSolverCompat/"

echo "== zip =="
cd "$OUT"
rm -f WatcherAdapter.zip CombatSolverCompat.zip
powershell.exe -NoProfile -Command "Compress-Archive -Path 'WatcherAdapter' -DestinationPath 'WatcherAdapter.zip' -Force" >/dev/null
powershell.exe -NoProfile -Command "Compress-Archive -Path 'CombatSolverCompat' -DestinationPath 'CombatSolverCompat.zip' -Force" >/dev/null

echo "== result =="
ls -la "$OUT"
