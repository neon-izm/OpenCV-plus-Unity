# OpenCV+Unity

An OpenCV native plugin for Unity. Built on **OpenCV 4.11**, this project compiles the managed layer and native wrapper (`OpenCvSharpExtern`) of the C# wrapper **OpenCvSharp 4.11** and distributes it as a UPM package.

A set of prebuilt native plugins for macOS / iOS / Android / Windows / Linux is built automatically on CI so it can run on real devices.

## Background

This project is based on the freely released OpenCV plugin **"OpenCV plus Unity"**.

- Original project (free version): <https://github.com/Gobra/OpenCV-Unity>
- It then follows the **OpenCV 4.11-based version** of the C# wrapper **OpenCvSharp**:
  - <https://github.com/shimat/opencvsharp> (pinned to tag `4.11.0.20250507`)

In other words, it inherits the ideas that "OpenCV plus Unity" (Gobra/OpenCV-Unity) provided — **Unity ⇔ OpenCV texture conversion, exception propagation, and Unity adaptation of the managed layer** — while updating **OpenCV / OpenCvSharp to 4.11** and making it buildable and verifiable on current Unity (6000.x).

## Features

- Latest native plugin based on **OpenCV 4.11 + OpenCvSharp 4.11**
- **Multi-platform** prebuilt binaries (macOS / iOS / Android / Windows / Linux)
- Distributed as a **UPM package** (`com.opencvplus.unity`), easy to reference from `Packages/manifest.json`
- **Unity adaptation of the managed layer**:
  - `NativeMethods.DllExtern` uses `__Internal` on iOS (links the static `.a` into the player)
  - Native exceptions are propagated to Unity via `redirectError` + `HandleException`
  - `GlobalUsings.cs` provides an equivalent for upstream's `ImplicitUsings` assumption in Unity
  - `OpenCvSharp.asmdef` isolates the managed layer into its own assembly
- Ships verification samples including an **AR marker demo** (ArUco) under `verify/`

## Layout

```
OpenCvPlusUnity/
├── source/
│   └── opencvsharp/          # OpenCvSharp 4.11.0.20250507 (submodule)
│       ├── opencv/           # OpenCV 4.11 source (auto checkout)
│       └── opencv_contrib/   # OpenCV contrib 4.11 (auto checkout)
├── Utils/                    # Build & packaging scripts
│   ├── build.sh              # OpenCV + OpenCvSharpExtern build orchestrator
│   ├── build-opencv.sh       # OpenCV itself
│   ├── build-extern.sh       # OpenCvSharpExtern
│   ├── package-unity.sh      # UPM packaging (cs11ify + plugins + templates)
│   ├── prune-extern.sh       # strip CUDA from extern sources
│   ├── cs11ify.py            # OpenCvSharp C#12 → C#11 rewrite
│   ├── opencv_options_unity.cmake
│   └── patches/              # local patches (applied automatically at build)
├── Unity/OpenCV+Unity/       # UPM package (com.opencvplus.unity)
│   └── Runtime/              # managed C# + native plugins
├── verify/UnityProject/      # verification Unity project (EditMode tests / AR demo)
└── bin/OpenCvSharpExtern-1.0/# build artifacts (per platform)
```

## Requirements

- **Unity 6000.x** (verified on `6000.3.10f1`; Unity 2022 is unsupported due to display-initialization issues)
- **macOS**: Apple Silicon (arm64) only / **iOS**: arm64 / **Android**: arm64-v8a, API 35, NDK 28+ / **Windows**: x86_64 / **Linux**: x86_64
- To build from source:
  - CMake 3.5+ (a local patch raises OpenCV's `cmake_minimum_required` to 3.5)
  - For Android, `ANDROID_NDK` must point to NDK 28+ (for Android 15 16KB page-size support)
  - Eigen3 (the `Eigen3::Eigen` target used by OpenCV)

## Building

Build the native plugins and stage the UPM package:

```bash
# 1. Build OpenCV + OpenCvSharpExtern (macos / ios / android / windows / linux)
ANDROID_NDK=$HOME/Library/Android/sdk/ndk/28.2.13676358 bash Utils/build.sh macos ios android

# 2. Stage the artifacts into the UPM package (Unity/OpenCV+Unity/)
bash Utils/package-unity.sh 1.0
```

### GitHub Actions

`.github/workflows/build.yml` builds all platforms on a `v*` tag push and automatically publishes the UPM package as a tarball in a **GitHub Release**.

## Using in a Unity project

Reference it as a UPM package (`com.opencvplus.unity`). After cloning this repository, you can reference it from `Packages/manifest.json` using a `file:` relative path:

```jsonc
// Packages/manifest.json
{
  "dependencies": {
    "com.opencvplus.unity": "file:<relative path>/Unity/OpenCV+Unity"
  }
}
```

Repository: <https://github.com/neon-izm/OpenCV-plus-Unity>

Alternatively, download the `opencvplus-unity-*.tgz` from a GitHub Release and extract it into `Packages/`.

The consumer side needs the following in `Assets/csc.rsp` (bundled by `package-unity.sh`):

```
-unsafe
-langversion:latest
```

## Verification

- A verification Unity project is included under `verify/UnityProject/`.
- EditMode tests verify OpenCV functionality (currently 53/53 green).
- **AR marker demo**: open `verify/UnityProject/Assets/Scenes/ARMarkerDemo.unity`, print `Assets/Markers/marker_0.png`, and point the camera at it to see an object rendered on the marker (ArUco / `OpenCvSharp.Aruco`).

## License

- This project's distributable (UPM package) is **Apache-2.0** (see `Unity/OpenCV+Unity/package.json`).
- OpenCV is **Apache-2.0**, and OpenCvSharp is **Apache-2.0**. Please refer to each project's license for details.
