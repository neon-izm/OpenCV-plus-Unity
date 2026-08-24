#!/bin/bash
# Orchestrator: build OpenCV + OpenCvSharpExtern for the requested platforms.
# Usage: build.sh <platform> [platform ...]
#   platform: macos | ios | android | windows
# Environment:
#   ANDROID_NDK  path to NDK r25+ (required for android)
#   OCV_VERSION  plugin version label (default 1.0)
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
VER="${OCV_VERSION:-1.0}"
PLATFORMS=("$@")
[[ ${#PLATFORMS[@]} -gt 0 ]] || { echo "specify at least one platform"; exit 1; }

for p in "${PLATFORMS[@]}"; do
  echo "================================================================"
  echo ">> Building OpenCV for $p"
  echo "================================================================"
  bash "$ROOT/Utils/build-opencv.sh" "$p" "$VER"
  echo "================================================================"
  echo ">> Building OpenCvSharpExtern for $p"
  echo "================================================================"
  bash "$ROOT/Utils/build-extern.sh" "$p" "$VER"
done

echo "All requested platforms built. Run Utils/package-unity.sh to stage into Unity."
