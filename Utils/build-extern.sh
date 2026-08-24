#!/bin/bash
# Build the (pruned) OpenCvSharpExtern wrapper for one Unity target platform.
# Usage: build-extern.sh <macos|ios|android|windows> [version]
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
OCS="$ROOT/source/opencvsharp"
EXT_SRC="$OCS/src/OpenCvSharpExtern"
PLATFORM="${1:?platform required: macos|ios|android|windows}"
VER="${2:-1.0}"

ART="$ROOT/build/artifacts/$PLATFORM"
STAGE="$ROOT/build/extern-stage-$PLATFORM"
OUT="$ROOT/bin/OpenCvSharpExtern-$VER/full/$PLATFORM"
JOBS="$(sysctl -n hw.ncpu 2>/dev/null || nproc 2>/dev/null || echo 4)"

rm -rf "$STAGE"
bash "$ROOT/Utils/prune-extern.sh" "$EXT_SRC" "$STAGE"
# Ensure the extern CMakeLists patch (Eigen3 target + OPENCVSHARP_BUILD_SHARED) is
# applied. The patch was generated against src/OpenCvSharpExtern/CMakeLists.txt;
# the staging dir holds CMakeLists.txt at its root, so strip 3 path components (-p3).
if ! grep -q "OPENCVSHARP_BUILD_SHARED" "$STAGE/CMakeLists.txt"; then
  echo "Applying opencvsharp-extern-cmake.patch to $STAGE/CMakeLists.txt"
  (cd "$STAGE" && patch -p3 < "$ROOT/Utils/patches/opencvsharp-extern-cmake.patch")
  if ! grep -q "OPENCVSHARP_BUILD_SHARED" "$STAGE/CMakeLists.txt"; then
    echo "patch(1) did not apply; retrying with git apply -p3"
    git -C "$STAGE" apply -p3 "$ROOT/Utils/patches/opencvsharp-extern-cmake.patch"
  fi
  if ! grep -q "OPENCVSHARP_BUILD_SHARED" "$STAGE/CMakeLists.txt"; then
    echo "ERROR: failed to apply opencvsharp-extern-cmake.patch (OPENCVSHARP_BUILD_SHARED missing)" >&2
    exit 1
  fi
  echo "extern CMakeLists patch applied"
fi

# Unity <-> OpenCV texture utils (utils.h/utils.cpp) are a local addition not
# present in the upstream submodule; copy the canonical files on a clean
# checkout (CI). Copying is more robust than applying a new-file patch.
if [ ! -f "$STAGE/utils.cpp" ]; then
  echo "Adding OpenCvSharpExtern utils (Unity texture conversion)"
  cp "$ROOT/Utils/patches/opencvsharp-extern-utils.h" "$STAGE/utils.h"
  cp "$ROOT/Utils/patches/opencvsharp-extern-utils.cpp" "$STAGE/utils.cpp"
  if [ ! -f "$STAGE/utils.cpp" ]; then
    echo "ERROR: failed to add extern utils (utils.cpp missing)" >&2
    exit 1
  fi
fi
mkdir -p "$STAGE/build" "$OUT"

case "$PLATFORM" in
  macos)
    # Apple Silicon (arm64) only.
    cmake -S "$STAGE" -B "$STAGE/build" \
      -D CMAKE_BUILD_TYPE=Release \
      -D CMAKE_PREFIX_PATH="$ART" \
      -D CMAKE_POLICY_VERSION_MINIMUM=3.5 \
      -D CMAKE_OSX_ARCHITECTURES="arm64" \
      -D CMAKE_OSX_DEPLOYMENT_TARGET=11.0
    cmake --build "$STAGE/build" -j"$JOBS"
    cp "$STAGE/build/libOpenCvSharpExtern.dylib" "$OUT/libOpenCvSharpExtern.dylib"
    # Canonical Unity macOS plugin format: wrap the dylib in a loadable bundle
    # (OpenCvSharpExtern.bundle/Contents/{MacOS,Info.plist,Resources}).
    BUNDLE="$OUT/OpenCvSharpExtern.bundle"
    mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
    mv "$OUT/libOpenCvSharpExtern.dylib" "$BUNDLE/Contents/MacOS/OpenCvSharpExtern"
    cp "$ROOT/Utils/toolchains/Info.plist" "$BUNDLE/Contents/Info.plist"
    echo "macOS arm64 dylib -> $BUNDLE/Contents/MacOS/OpenCvSharpExtern"
    ;;
  ios)
    cmake -S "$STAGE" -B "$STAGE/build" \
      -D CMAKE_PREFIX_PATH="$ART" \
      -D OpenCV_DIR="$ART/lib/cmake/opencv4" \
      -D CMAKE_POLICY_VERSION_MINIMUM=3.5 \
      -D CMAKE_TOOLCHAIN_FILE="$ROOT/Utils/toolchains/toolchain-ios.cmake" \
      -D OPENCVSHARP_BUILD_SHARED=OFF
    cmake --build "$STAGE/build" -j"$JOBS"
    cp "$STAGE/build/libOpenCvSharpExtern.a" "$OUT/libOpenCvSharpExtern.a"
    # iOS: the extern is static, so merge it together with ALL OpenCV static libs
    # (+ 3rdparty) into ONE self-contained .a (reference-repo approach via
    # libtool -static). Unity links a single library for the iOS player.
    libtool -static -o "$OUT/libOpenCvSharpExtern.merged.a" \
      "$OUT/libOpenCvSharpExtern.a" \
      "$ART"/lib/libopencv_*.a \
      "$ART"/lib/opencv4/3rdparty/*.a
    mv "$OUT/libOpenCvSharpExtern.merged.a" "$OUT/libOpenCvSharpExtern.a"
    echo "iOS static lib (merged: extern + OpenCV + 3rdparty) -> $OUT/libOpenCvSharpExtern.a"
    ;;
  android)
    : "${ANDROID_NDK:?set ANDROID_NDK to your NDK r25+ path}"
    cmake -S "$STAGE" -B "$STAGE/build" \
      -D CMAKE_PREFIX_PATH="$ART" \
      -D OpenCV_DIR="$ART/sdk/native/jni/abi-arm64-v8a" \
      -D CMAKE_POLICY_VERSION_MINIMUM=3.5 \
      -D CMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
      -D ANDROID_ABI=arm64-v8a -D ANDROID_PLATFORM=android-35 \
      -D ANDROID_STL=c++_shared \
      -D OPENCVSHARP_BUILD_SHARED=ON
    cmake --build "$STAGE/build" -j"$JOBS"
    cp "$STAGE/build/libOpenCvSharpExtern.so" "$OUT/libOpenCvSharpExtern.so"
    # strip debug info (keeps exported symbols) to shrink the .so dramatically
    # NDK prebuilt tools live under a host-tag subdir (darwin-arm64 on Apple
    # Silicon runners, linux-x86_64 on ubuntu, darwin-x86_64 on Intel macOS).
    # Some NDK versions ship only darwin-x86_64 even on arm64 hosts -> fall back.
    HOST_TAG=linux-x86_64
    case "$(uname -s)/$(uname -m)" in
      Darwin/arm64)   HOST_TAG=darwin-arm64 ;;
      Darwin/x86_64)  HOST_TAG=darwin-x86_64 ;;
      Linux/aarch64)  HOST_TAG=linux-aarch64 ;;
    esac
    OLD_TAG="$HOST_TAG"
    if [ ! -d "$ANDROID_NDK/toolchains/llvm/prebuilt/$HOST_TAG" ]; then
      HOST_TAG=$(ls "$ANDROID_NDK/toolchains/llvm/prebuilt" | head -1)
      echo "NDK prebuilt $OLD_TAG not found, using $HOST_TAG"
    fi
    "$ANDROID_NDK/toolchains/llvm/prebuilt/$HOST_TAG/bin/llvm-strip" -g "$OUT/libOpenCvSharpExtern.so"
    echo "Android arm64-v8a .so -> $OUT/libOpenCvSharpExtern.so"
    ;;
  windows)
    # OpenCV on Windows may install its working OpenCVConfig.cmake to
    # <prefix>/staticlib (when the MSVC runtime tag is not recognized and the
    # install prefix is empty), otherwise the standard lib/cmake/opencv4.
    OCV_CFG="$ART/lib/cmake/opencv4"
    [ -f "$OCV_CFG/OpenCVConfig.cmake" ] || OCV_CFG="$ART/staticlib"
    [ -f "$OCV_CFG/OpenCVConfig.cmake" ] || OCV_CFG="$ART"
    echo "Using OpenCV_DIR=$OCV_CFG"
    cmake -S "$STAGE" -B "$STAGE/build" \
      -D CMAKE_PREFIX_PATH="$ART" \
      -D OpenCV_DIR="$OCV_CFG" \
      -D CMAKE_POLICY_VERSION_MINIMUM=3.5 \
      -D CMAKE_BUILD_TYPE=Release \
      -D CMAKE_CXX_FLAGS_RELEASE="-MT" \
      -G Ninja
    cmake --build "$STAGE/build" -j"$JOBS"
    cp "$STAGE/build/OpenCvSharpExtern.dll" "$OUT/OpenCvSharpExtern.dll"
    echo "Windows x86_64 dll -> $OUT/OpenCvSharpExtern.dll"
    ;;
  linux)
    cmake -S "$STAGE" -B "$STAGE/build" \
      -D CMAKE_BUILD_TYPE=Release \
      -D CMAKE_PREFIX_PATH="$ART" \
      -D CMAKE_POLICY_VERSION_MINIMUM=3.5
    cmake --build "$STAGE/build" -j"$JOBS"
    cp "$STAGE/build/libOpenCvSharpExtern.so" "$OUT/libOpenCvSharpExtern.so"
    echo "Linux x86_64 .so -> $OUT/libOpenCvSharpExtern.so"
    ;;
  *) echo "unknown platform $PLATFORM"; exit 1 ;;
esac
