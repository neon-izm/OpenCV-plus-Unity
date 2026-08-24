#!/bin/bash
# Build OpenCV 5.x (slim, major modules) for one Unity target platform.
# Usage: build-opencv.sh <macos|ios|android|windows> [version]
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
OCS="$ROOT/source/opencvsharp"
OCV="$OCS/opencv"
OCV_CONTRIB="$OCS/opencv_contrib"
OPT="$ROOT/Utils/opencv_options_unity.cmake"
PLATFORM="${1:?platform required: macos|ios|android|windows}"
VER="${2:-1.0}"

ART="$ROOT/build/artifacts/$PLATFORM"
BLD="$ROOT/build/opencv-$PLATFORM"
mkdir -p "$BLD" "$ART"

# OpenCV + opencv_contrib sources live under source/opencvsharp. Acquire them at
# 4.11.0 if not already present (fresh checkout / CI).
if [ ! -f "$OCV/CMakeLists.txt" ]; then
  git clone --depth 1 --branch 4.11.0 https://github.com/opencv/opencv.git "$OCV"
fi
if [ ! -d "$OCV_CONTRIB/modules" ]; then
  git clone --depth 1 --branch 4.11.0 https://github.com/opencv/opencv_contrib.git "$OCV_CONTRIB"
fi

# Apply local build patches (idempotent).
apply_patch() {
  local repo="$1" patch="$2"
  if git -C "$repo" apply --reverse --check "$patch" 2>/dev/null; then
    : # already applied
  elif git -C "$repo" apply --check "$patch" 2>/dev/null; then
    git -C "$repo" apply "$patch"
  else
    echo "WARN: patch $patch does not apply cleanly in $repo"
  fi
}
apply_patch "$OCV"         "$ROOT/Utils/patches/opencv-cmake-min-3.5.patch"
apply_patch "$OCV_CONTRIB" "$ROOT/Utils/patches/opencv-contrib-superres-ios.patch"

COMMON=(-C "$OPT"
  -D OPENCV_EXTRA_MODULES_PATH="$OCV_CONTRIB/modules"
  -D CMAKE_INSTALL_PREFIX="$ART"
  -D BUILD_SHARED_LIBS=OFF
  -D CMAKE_POLICY_VERSION_MINIMUM=3.5
  # Use the bundled libpng/zlib exclusively. On macOS the system Finder can
  # pick up /Library/Frameworks/Mono.framework/Headers (libpng 1.4) as
  # PNG_PNG_INCLUDE_DIR, which ABI-mismatches the bundled libpng and silently
  # breaks PNG encode/decode. Pin the include dirs to the bundled sources and
  # disable libpng NEON (known to produce corrupt output on some arm64 builds).
  -D PNG_PNG_INCLUDE_DIR="$OCV/3rdparty/libpng"
  -D PNG_INCLUDE_DIR="$OCV/3rdparty/libpng"
  -D PNG_ARM_NEON=off
  -D ZLIB_INCLUDE_DIR="$OCV/3rdparty/zlib"
  # Re-enable the contrib text module explicitly; reconfiguring can otherwise
  # auto-disable it (OpenCvPlus 3.3-era compat).
  -D BUILD_opencv_text=ON)

JOBS="$(sysctl -n hw.ncpu 2>/dev/null || nproc 2>/dev/null || echo 4)"
CONFIG=""

case "$PLATFORM" in
  macos)
    # Apple Silicon (arm64) only — Intel Mac is no longer supported.
    cmake -S "$OCV" -B "$BLD" "${COMMON[@]}" \
      -G "Unix Makefiles" \
      -D CMAKE_OSX_ARCHITECTURES="arm64" \
      -D CMAKE_OSX_DEPLOYMENT_TARGET=11.0
    ;;
  ios)
    # Must be an env var so it propagates into CMake's try_compile subprojects.
    export IPHONEOS_DEPLOYMENT_TARGET=15.0
    # Xcode generator is multi-config; build/install Release explicitly.
    CONFIG=Release
    cmake -S "$OCV" -B "$BLD" "${COMMON[@]}" \
      -G Xcode \
      -D CMAKE_TOOLCHAIN_FILE="$OCV/platforms/ios/cmake/Toolchains/Toolchain-iPhoneOS_Xcode.cmake" \
      -D IOS_ARCH=arm64
    ;;
  android)
    : "${ANDROID_NDK:?set ANDROID_NDK to your NDK r25+ path}"
    # OpenCV 4.11 uses the NDK's own CMake toolchain (android.toolchain.cmake was removed).
    cmake -S "$OCV" -B "$BLD" "${COMMON[@]}" \
      -D CMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
      -D ANDROID_ABI=arm64-v8a -D ANDROID_PLATFORM=android-35 \
      -D ANDROID_STL=c++_shared
    ;;
  windows)
    # Ninja + MSVC (cl via the job's msvc-dev-cmd env). Avoids depending on a
    # specific Visual Studio version/generator name on the runner.
    cmake -S "$OCV" -B "$BLD" "${COMMON[@]}" \
      -G Ninja \
      -D CMAKE_BUILD_TYPE=Release
    ;;
  linux)
    cmake -S "$OCV" -B "$BLD" "${COMMON[@]}" \
      -G "Unix Makefiles"
    ;;
  *) echo "unknown platform $PLATFORM"; exit 1 ;;
esac

cmake --build "$BLD" -j"$JOBS" ${CONFIG:+--config $CONFIG}
cmake --install "$BLD" ${CONFIG:+--config $CONFIG}
echo "OpenCV ($PLATFORM) installed -> $ART"
