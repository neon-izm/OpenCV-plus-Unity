# iOS toolchain for OpenCvSharpExtern (arm64, static).
# Relies on Apple Clang / Xcode. OpenCV is built separately with its own
# platforms/ios/cmake/Toolchains/Toolchain-iPhoneOS_Xcode.cmake; this file is
# only for the wrapper library.

set(CMAKE_SYSTEM_NAME iOS)
set(CMAKE_OSX_SYSROOT iphoneos)
set(CMAKE_OSX_ARCHITECTURES arm64 CACHE STRING "" FORCE)
set(CMAKE_TARGET_ARCHITECTURES arm64)

set(ENABLE_BITCODE OFF CACHE BOOL "" FORCE)
set(CMAKE_FRAMEWORK OFF CACHE BOOL "" FORCE)

# C++ standard
set(CMAKE_CXX_STANDARD 17 CACHE STRING "" FORCE)
set(CMAKE_CXX_STANDARD_REQUIRED ON CACHE BOOL "" FORCE)
