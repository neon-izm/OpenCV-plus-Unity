using System;
using System.Runtime.InteropServices;
using OpenCvSharp.Internal;

namespace OpenCvSharp
{
    /// <summary>
    /// Unity &lt;-&gt; OpenCV texture conversion helpers (ported from the reference
    /// OpenCV-plus-Unity wrapper; native side is utils.h/utils.cpp in the extern).
    /// </summary>
    public static class Unity
    {
        [DllImport(NativeMethods.DllExtern, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr utils_texture_to_mat(
            IntPtr pixels32, int w, int h,
            [MarshalAs(UnmanagedType.I1)] bool flipVertically,
            [MarshalAs(UnmanagedType.I1)] bool flipHorizontally,
            int rotationAngle);

        [DllImport(NativeMethods.DllExtern, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr utils_mat_to_texture_2(IntPtr mat);

        [StructLayout(LayoutKind.Explicit)]
        private struct Color32Bytes
        {
            [FieldOffset(0)]
            public byte[] byteArray;

            [FieldOffset(0)]
            public UnityEngine.Color32[] colors;
        }

        /// <summary>
        /// Converts a Unity Texture2D into an OpenCV Mat (RGBA -> BGR, vertical flip applied).
        /// </summary>
        public static Mat TextureToMat(UnityEngine.Texture2D texture,
            bool flipVertically = false, bool flipHorizontally = false, int rotationAngle = 0)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));
            return PixelsToMat(texture.GetPixels32(), texture.width, texture.height,
                flipVertically, flipHorizontally, rotationAngle);
        }

        /// <summary>
        /// Converts an RGBA32 pixel buffer into an OpenCV Mat.
        /// </summary>
        public static Mat PixelsToMat(UnityEngine.Color32[] pixels32, int width, int height,
            bool flipVertically = false, bool flipHorizontally = false, int rotationAngle = 0)
        {
            if (pixels32 == null)
                throw new ArgumentNullException(nameof(pixels32));
            if (rotationAngle != 0 && rotationAngle != 90 && rotationAngle != 180 && rotationAngle != 270)
                throw new ArgumentException($"rotationAngle must be in {{ 0, 90, 180, 270 }} but was {rotationAngle}");

            GCHandle handle = GCHandle.Alloc(pixels32, GCHandleType.Pinned);
            try
            {
                IntPtr matPtr = utils_texture_to_mat(handle.AddrOfPinnedObject(), width, height,
                    !flipVertically, flipHorizontally, rotationAngle);
                return Mat.FromNativePointer(matPtr);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Converts an OpenCV Mat into a Unity Texture2D (BGR -> RGBA, vertical flip applied).
        /// </summary>
        public static unsafe UnityEngine.Texture2D MatToTexture(Mat mat, UnityEngine.Texture2D outTexture = null)
        {
            if (mat == null)
                throw new ArgumentNullException(nameof(mat));
            Size size = mat.Size();
            using (Mat unityMat = new Mat(utils_mat_to_texture_2(mat.CvPtr)))
            {
                if (outTexture == null || outTexture.width != size.Width || outTexture.height != size.Height)
                    outTexture = new UnityEngine.Texture2D(size.Width, size.Height);

                int count = size.Width * size.Height;
                Color32Bytes data = new Color32Bytes();
                data.byteArray = new byte[count * 4];
                data.colors = new UnityEngine.Color32[count];
                Marshal.Copy((IntPtr)unityMat.DataPointer, data.byteArray, 0, data.byteArray.Length);
                outTexture.SetPixels32(data.colors);
                outTexture.Apply();
                return outTexture;
            }
        }
    }
}