# CMake initial cache for OpenCV 4.x Unity build.
# Strategy: build (almost) all modules so the OpenCvSharpExtern wrapper compiles
# against every type it references, and only exclude what cannot be built here:
#   - cuda (GPU; not available on iOS/Android/macOS metal targets)
#   - text (requires Tesseract OCR, which we do not provide)
# Eigen3 is kept ON; the wrapper provides the Eigen3::Eigen target on macOS.
# Used via: cmake -C Utils/opencv_options_unity.cmake -S opencv -B build/opencv-<plat> ...
# Platform/install/toolchain settings are passed by Utils/build-opencv.sh.

set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
set(BUILD_SHARED_LIBS    OFF    CACHE BOOL "" FORCE)
set(ENABLE_CXX11         ON     CACHE BOOL "" FORCE)

# Exclude only what we cannot build / do not provide.
set(BUILD_opencv_cuda         OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudacodec    OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudafeatures2d OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudafilters  OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudalegacy   OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudaoptflow  OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudastereo   OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_cudawarping  OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_hdf          OFF CACHE BOOL "" FORCE)
# viz (VTK) and sfm (GLog/GFlags) are niche 3D modules that fail to build with
# current toolchains / are unneeded for the Unity wrapper.
set(BUILD_opencv_viz          OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_sfm          OFF CACHE BOOL "" FORCE)

# No code generators / tests / extras
set(BUILD_EXAMPLES   OFF CACHE BOOL "" FORCE)
set(BUILD_ANDROID_EXAMPLES OFF CACHE BOOL "" FORCE)
set(INSTALL_ANDROID_EXAMPLES OFF CACHE BOOL "" FORCE)
set(INSTALL_PYTHON_EXAMPLES OFF CACHE BOOL "" FORCE)
set(INSTALL_C_EXAMPLES OFF CACHE BOOL "" FORCE)
set(BUILD_DOCS       OFF CACHE BOOL "" FORCE)
set(BUILD_PERF_TESTS OFF CACHE BOOL "" FORCE)
set(BUILD_TESTS      OFF CACHE BOOL "" FORCE)
set(BUILD_JAVA       OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_apps OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_js   OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_python_tests OFF CACHE BOOL "" FORCE)
set(BUILD_opencv_ts   OFF CACHE BOOL "" FORCE)

# No Tesseract / GStreamer / FFMPEG (keep videoio lightweight: image + basic)
set(WITH_TESSERACT OFF CACHE BOOL "" FORCE)
set(WITH_GSTREAMER OFF CACHE BOOL "" FORCE)
set(WITH_FFMPEG    OFF CACHE BOOL "" FORCE)
set(WITH_GTK       OFF CACHE BOOL "" FORCE)
set(WITH_ADE       OFF CACHE BOOL "" FORCE)
set(WITH_PROTOBUF  OFF CACHE BOOL "" FORCE)
set(BUILD_PROTOBUF OFF CACHE BOOL "" FORCE)

# OpenEXR pulls an exported OpenEXR::OpenEXR target that is not available to the
# wrapper's find_package(OpenCV), breaking the consumer configure step.
set(WITH_OPENEXR OFF CACHE BOOL "" FORCE)

# HDF5 is only needed by the (niche) opencv_hdf module, which the wrapper does
# not use; disabling it avoids the dangling hdf5-shared link dependency.
set(WITH_HDF5 OFF CACHE BOOL "" FORCE)

# Harmless on OpenCV 4.x (option does not exist there); kept for 5.x parity.
set(WITH_KLEIDICV OFF CACHE BOOL "" FORCE)
