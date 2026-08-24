#ifndef _CPP_UTILS_H_
#define _CPP_UTILS_H_

#include "include_opencv.h"

// Unity <-> OpenCV texture conversion helpers (ported from the reference
// OpenCV-plus-Unity wrapper). Uses OpenCV 4 cv::COLOR_* constants.

/// <summary>
/// Converts a Unity RGBA32 pixel buffer into an OpenCV BGR Mat.
/// </summary>
CVAPI(cv::Mat*) utils_texture_to_mat(unsigned char *pixels32, int w, int h, bool flipVertically, bool flipHorizontally, int rotationAngle)
{
    cv::Mat input(h, w, CV_8UC4, pixels32);
    cv::Mat bgr(h, w, CV_8UC3);
    cv::cvtColor(input, bgr, cv::COLOR_RGBA2BGR);
    cv::Mat* output = new cv::Mat(h, w, CV_8UC3);

    switch (rotationAngle)
    {
    case 90:
    case -270:
        bgr = bgr.t();
        flipVertically = !flipVertically;
        break;
    case 180:
    case -180:
        flipVertically = !flipVertically;
        flipHorizontally = !flipHorizontally;
        break;
    case 270:
    case -90:
        bgr = bgr.t();
        flipHorizontally = !flipHorizontally;
        break;
    }

    if (flipVertically || flipHorizontally)
    {
        // 0 -> flip vertically, 1+ -> flip horizontally, -1 -> flip both
        int flipCode = (flipVertically && flipHorizontally) ? -1 : (flipVertically ? 0 : 1);
        cv::flip(bgr, *output, flipCode);
    }
    else
    {
        *output = bgr;
    }

    return output;
}

/// <summary>
/// Converts an OpenCV Mat to an RGBA Mat for Unity, applying the given color conversion.
/// </summary>
CVAPI(cv::Mat*) utils_mat_to_texture_1(cv::Mat *mat, int colorConversionCode)
{
    cv::Mat* output = new cv::Mat(mat->size(), CV_8UC4);
    cv::Mat flipped(mat->size(), mat->type());
    cv::flip(*mat, flipped, 0);
    cv::cvtColor(flipped, *output, colorConversionCode);
    return output;
}

/// <summary>
/// Converts an OpenCV Mat to an RGBA Mat for Unity (auto-selects color conversion).
/// </summary>
CVAPI(cv::Mat*) utils_mat_to_texture_2(cv::Mat *mat)
{
    int code = cv::COLOR_BGR2RGBA;
    if (mat->channels() == 1)
    {
        code = cv::COLOR_GRAY2RGBA;
    }
    return utils_mat_to_texture_1(mat, code);
}

#endif