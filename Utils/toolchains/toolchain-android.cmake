# Android toolchain for OpenCvSharpExtern (arm64-v8a, API 35).
# Uses the NDK's clang. Point -DANDROID_NDK=<ndk> and -DANDROID_PLATFORM=android-35
# from the caller. Based on the standard android-ndk clang cross setup.

set(CMAKE_SYSTEM_NAME Android)
set(CMAKE_SYSTEM_VERSION 35)
set(ANDROID_PLATFORM android-35 CACHE STRING "" FORCE)
set(ANDROID_ABI arm64-v8a CACHE STRING "" FORCE)

set(CMAKE_ANDROID_ARCH_ABI arm64-v8a)
set(CMAKE_ANDROID_NDK_TOOLCHAIN_VERSION clang)

# C++17 for OpenCV 5
set(CMAKE_CXX_STANDARD 17 CACHE STRING "" FORCE)
set(CMAKE_CXX_STANDARD_REQUIRED ON CACHE BOOL "" FORCE)
set(CMAKE_C_VISIBILITY_PRESET hidden CACHE STRING "" FORCE)
set(CMAKE_CXX_VISIBILITY_PRESET hidden CACHE STRING "" FORCE)

# Static C++ runtime, shared STL (c++_shared) to match Unity/NDK
set(ANDROID_STL c++_shared CACHE STRING "" FORCE)
