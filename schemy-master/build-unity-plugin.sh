#!/usr/bin/env bash
# Build schemy.dll and copy it to Engine/Plugins for Unity.
# Run from anywhere inside the ssnoir repo.

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CSPROJ="$SCRIPT_DIR/src/schemy/schemy.csproj"
PLUGINS_DIR="$REPO_ROOT/Engine/Plugins"

echo "Building schemy (netstandard2.0, Release)..."
dotnet build "$CSPROJ" -c Release --nologo -v q

DLL="$SCRIPT_DIR/src/schemy/bin/Release/netstandard2.0/schemy.dll"
echo "Copying $DLL → $PLUGINS_DIR/schemy.dll"
cp "$DLL" "$PLUGINS_DIR/schemy.dll"

echo "Done. Unity will pick up the new dll on next domain reload."
