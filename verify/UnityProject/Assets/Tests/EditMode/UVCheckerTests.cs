using System;
using System.IO;
using NUnit.Framework;
using OpenCvSharp;
using UnityEditor;
using UnityEngine;

namespace OpenCvSharp.Tests
{
    /// <summary>
    /// Tests that run OpenCV operations against the user-provided test texture
    /// "CustomUVChecker_byValle_1K" (Assets/CustomUVChecker_byValle_1K.png).
    /// Each processing test also writes the resulting Mat to
    /// &lt;project&gt;/TestResults/UVChecker/*.png for visual inspection.
    /// </summary>
    [TestFixture]
    public class UVCheckerTests
    {
        private static string SaveDir()
        {
            string dir = Path.Combine(Application.dataPath, "..", "TestResults", "UVChecker");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        private static void SavePng(Mat mat, string name)
        {
            string path = Path.Combine(SaveDir(), name + ".png");
            Assert.IsTrue(Cv2.ImWrite(path, mat), "ImWrite failed: " + path);
            TestContext.Out.WriteLine("Saved: " + path);
        }

        private static Texture2D LoadChecker()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CustomUVChecker_byValle_1K.png");
            Assert.IsNotNull(tex, "CustomUVChecker_byValle_1K.png not found");
            return tex;
        }

        [Test]
        public void TextureDimensions()
        {
            var tex = LoadChecker();
            Assert.AreEqual(1024, tex.width);
            Assert.AreEqual(1024, tex.height);
        }

        [Test]
        public void TextureToMat()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            Assert.AreEqual(1024, mat.Width);
            Assert.AreEqual(1024, mat.Height);
            Assert.AreEqual(3, mat.Channels());
            SavePng(mat, "01_texture_to_mat");
        }

        [Test]
        public void MatToTextureRoundTrip()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            var outTex = Unity.MatToTexture(mat);
            Assert.AreEqual(tex.width, outTex.width);
            Assert.AreEqual(tex.height, outTex.height);
            Assert.AreEqual(1024, outTex.width);
        }

        [Test]
        public void Grayscale()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            Assert.AreEqual(1, gray.Channels());
            Assert.AreEqual(1024, gray.Width);
            SavePng(gray, "02_grayscale");
        }

        [Test]
        public void BinarizeChecker()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            using var gray = new Mat();
            using var bin = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 127, 255, ThresholdTypes.Binary);
            Assert.AreEqual(1024, bin.Width);
            var p0 = bin.At<byte>(0, 0);
            Assert.IsTrue(p0 == 0 || p0 == 255);
            SavePng(bin, "03_binarize");
        }

        [Test]
        public void FindContoursChecker()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            using var bin = new Mat();
            Cv2.Threshold(gray, bin, 127, 255, ThresholdTypes.Binary);
            Cv2.FindContours(bin, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
            Assert.Greater(contours.Length, 0);
            Cv2.DrawContours(mat, contours, -1, new Scalar(0, 0, 255), 2);
            SavePng(mat, "04_contours");
        }

        [Test]
        public void GaussianBlurChecker()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            using var blur = new Mat();
            Cv2.GaussianBlur(mat, blur, new Size(11, 11), 0);
            Assert.AreEqual(mat.Size(), blur.Size());
            SavePng(blur, "05_gaussian_blur");
        }

        [Test]
        public void SobelChecker()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            using var sobel = new Mat();
            Cv2.Sobel(gray, sobel, MatType.CV_8UC1, 0, 1);
            Assert.AreEqual(gray.Size(), sobel.Size());
            SavePng(sobel, "06_sobel");
        }

        [Test]
        public void FlipChecker()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            using var flipped = new Mat();
            Cv2.Flip(mat, flipped, FlipMode.Y);
            Assert.AreEqual(mat.Size(), flipped.Size());
            SavePng(flipped, "07_flip");
        }

        [Test]
        public void SplitChannelsChecker()
        {
            var tex = LoadChecker();
            using var mat = Unity.TextureToMat(tex);
            Mat[] layers = Cv2.Split(mat);
            Assert.AreEqual(3, layers.Length);
            SavePng(layers[2], "08_r_channel");
            SavePng(layers[1], "08_g_channel");
            SavePng(layers[0], "08_b_channel");
            foreach (var l in layers) l.Dispose();
        }
    }
}