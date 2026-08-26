#!/bin/bash
# Stage built native libraries and the managed OpenCvSharp C# into the Unity UPM
# package under Unity/OpenCV+Unity/ (Runtime/ + package.json).
# Usage: package-unity.sh [version]
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
VER="${1:-1.0}"
BIN="$ROOT/bin/OpenCvSharpExtern-$VER/full"
PKG="$ROOT/Unity/OpenCV+Unity"
RT="$PKG/Runtime"
OCS="$ROOT/source/opencvsharp"

mkdir -p "$RT/Plugins/macOS" "$RT/Plugins/iOS" \
         "$RT/Plugins/Android/arm64-v8a" "$RT/Plugins/Windows/x86_64" \
         "$RT/Plugins/Linux/x86_64"

[[ -d "$BIN/macos/OpenCvSharpExtern.bundle" ]] && \
  rm -rf "$RT/Plugins/macOS/arm64/OpenCvSharpExtern.bundle" && \
  mkdir -p "$RT/Plugins/macOS/arm64" && \
  cp -R "$BIN/macos/OpenCvSharpExtern.bundle" "$RT/Plugins/macOS/arm64/"
[[ -f "$BIN/ios/libOpenCvSharpExtern.a" ]] && \
  cp "$BIN/ios/libOpenCvSharpExtern.a" "$RT/Plugins/iOS/"
[[ -f "$BIN/android/libOpenCvSharpExtern.so" ]] && \
  cp "$BIN/android/libOpenCvSharpExtern.so" "$RT/Plugins/Android/arm64-v8a/"
[[ -f "$BIN/windows/OpenCvSharpExtern.dll" ]] && \
  cp "$BIN/windows/OpenCvSharpExtern.dll" "$RT/Plugins/Windows/x86_64/"
[[ -f "$BIN/linux/libOpenCvSharpExtern.so" ]] && \
  cp "$BIN/linux/libOpenCvSharpExtern.so" "$RT/Plugins/Linux/x86_64/"

# Managed OpenCvSharp C# (matches the extern version)
SRC_CS="$OCS/src/OpenCvSharp"
if [[ -d "$SRC_CS" ]]; then
  rm -rf "$RT"/{Cv2,Fundamentals,Internal,Modules,Properties}
  mkdir -p "$RT"
  cp -R "$SRC_CS/Cv2" "$SRC_CS/Fundamentals" "$SRC_CS/Internal" "$SRC_CS/Modules" "$RT/"
  # Drop .NET-only build artifacts (not used by Unity).
  rm -rf "$RT/Properties"
  # Rewrite C# 12 constructs (primary ctors, collection expressions) to C# 11 so
  # the code compiles in Unity (Roslyn caps at C# 11).
  python3 "$ROOT/Utils/cs11ify.py" "$RT"
  # IL2CPP cannot resolve `ref readonly` returns (T& modreq(InAttribute)) on iOS,
  # so ReadOnlyArray2D's indexer must return by value for the player build to succeed.
  python3 - "$RT/Internal/Util/ReadOnlyArray2D.cs" <<'PY'
import sys
path = sys.argv[1]
text = open(path).read()
text = text.replace(
    "public ref readonly T this[int index0, int index1] => ref data[index0, index1];",
    "public T this[int index0, int index1] => data[index0, index1];")
open(path, "w").write(text)
PY
  # Unity / IL2CPP adaptations for native error callbacks and iOS DllImport:
  # lambdas cannot be marshaled to native under IL2CPP; iOS IsUnix() is unreliable.
  python3 "$ROOT/Utils/patch-nativemethods-unity.py" \
    "$RT/Internal/PInvoke/NativeMethods/NativeMethods.cs"
  # Replace upstream ExceptionHandler (#if DOTNETCORE + anonymous delegate) with the
  # Unity-ready static MonoPInvokeCallback implementation, and ship the attribute.
  cp "$ROOT/Utils/unity-template/ExceptionHandler.cs" \
    "$RT/Internal/PInvoke/ExceptionHandler.cs"
  cp "$ROOT/Utils/unity-template/MonoPInvokeCallbackAttribute.cs" \
    "$RT/Internal/PInvoke/MonoPInvokeCallbackAttribute.cs"
  # Unity-specific files that are not present in the upstream OpenCvSharp source:
  #  - GlobalUsings.cs : upstream relies on <ImplicitUsings>enable</ImplicitUsings>,
  #    which Unity's compiler does not accept via csc.rsp (-implicitusings:enable
  #    is rejected), so the equivalent `global using` directives are shipped here.
  #  - OpenCvSharp.asmdef : isolates the managed wrapper into its own assembly so
  #    Unity test assemblies (and user asmdefs) can reference it by name.
  cp "$ROOT/Utils/unity-template/GlobalUsings.cs" "$RT/GlobalUsings.cs"
  cp "$ROOT/Utils/unity-template/OpenCvSharp.asmdef" "$RT/OpenCvSharp.asmdef"
  cp -R "$ROOT/Utils/unity-template/Unity" "$RT/"
  echo "Managed OpenCvSharp C# copied -> $RT"
fi

# Per-assembly csc.rsp alongside OpenCvSharp.asmdef: Unity applies a csc.rsp that
# sits next to an .asmdef to that assembly's compilation (a package-root csc.rsp
# is NOT honored). Needed for file-scoped namespaces / global using (C#10+).
printf -- '-unsafe\n-langversion:latest\n' > "$RT/csc.rsp"

echo "Staged UPM package at $PKG"