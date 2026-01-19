#!/usr/bin/env bash
set -euo pipefail

# build.sh - Build and package AmbilightSmoothed plugin (POSIX)
# Usage: ./build.sh [Configuration]
# Default Configuration: Release

CONFIG=${1:-Release}
PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT_FILE="$PROJECT_ROOT/Artemis.Plugins.LayerBrushes.AmbilightSmoothed.csproj"
OUTPUT_DIR="$PROJECT_ROOT/bin/$CONFIG/net10.0-windows"
ZIP_OUTPUT_DIR="$PROJECT_ROOT/bin/$CONFIG"
ZIP_FILE="$ZIP_OUTPUT_DIR/AmbilightSmoothed.zip"
TEMP_DIR="$ZIP_OUTPUT_DIR/temp_zip"

echo "=== Building AmbilightSmoothed Plugin ==="
echo "Project Root: $PROJECT_ROOT"
echo "Configuration: $CONFIG"

echo "Building project..."
dotnet build "$PROJECT_FILE" -c "$CONFIG"

if [ $? -ne 0 ]; then
  echo "Build failed" >&2
  exit 1
fi

if [ ! -d "$OUTPUT_DIR" ]; then
  echo "Expected output directory not found: $OUTPUT_DIR" >&2
  echo "If the project targets a windows RID, ensure the build produced files or adjust TargetFramework for local Linux builds." >&2
  exit 1
fi

echo "Creating plugin package..."
rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

# Copy output files to temp dir
cp -r "$OUTPUT_DIR"/* "$TEMP_DIR/" || true

# Remove existing zip if present
if [ -f "$ZIP_FILE" ]; then rm -f "$ZIP_FILE"; fi

# Create zip (prefer system 'zip', fallback to python3)
if command -v zip >/dev/null 2>&1; then
  (cd "$TEMP_DIR" && zip -r "$ZIP_FILE" .)
else
  python3 - <<PY
import os, zipfile
root = os.path.abspath("$TEMP_DIR")
zf = zipfile.ZipFile(os.path.abspath("$ZIP_FILE"), "w", compression=zipfile.ZIP_DEFLATED)
for dirpath, dirs, files in os.walk(root):
    for f in files:
        full = os.path.join(dirpath, f)
        arc = os.path.relpath(full, root)
        zf.write(full, arc)
zf.close()
PY
fi

rm -rf "$TEMP_DIR"

echo "Package created: $ZIP_FILE"
