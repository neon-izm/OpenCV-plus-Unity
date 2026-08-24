using System;
using NUnit.Framework;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using UnityEngine;

namespace OpenCvSharp.Tests
{
    /// <summary>
    /// Ports of the 20 samples from "OpenCV plus Unityサンプル集 20選"
    /// https://nn-hokuson.hatenablog.com/entry/2021/04/26/103948
    /// as EditMode tests (run the OpenCV operation through Unity.TextureToMat /
    /// Unity.MatToTexture where the sample does).
    /// </summary>
    [TestFixture]
    public class HokusonSamplesTests
    {
        private static Texture2D CreateTexture(int w, int h, Func<int, int, Color32> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Mat MakeBgr(int w, int h, byte b = 30, byte g = 30, byte r = 30)
        {
            return new Mat(h, w, MatType.CV_8UC3, new Scalar(b, g, r));
        }

        private static Mat MakeGray(int w, int h, byte v = 128)
        {
            return new Mat(h, w, MatType.CV_8UC1, new Scalar(v));
        }

        // ---------------------------------------------------------------
        // 1. 画像の読み込み (Texture2D <-> Mat)
        // ---------------------------------------------------------------
        [Test]
        public void LoadAndConvert()
        {
            var tex = CreateTexture(32, 24, (x, y) => new Color32((byte)x, (byte)y, 255, 255));
            using var mat = Unity.TextureToMat(tex);
            Assert.AreEqual(24, mat.Height);
            Assert.AreEqual(32, mat.Width);
            Assert.AreEqual(3, mat.Channels());

            var outTex = Unity.MatToTexture(mat);
            Assert.AreEqual(32, outTex.width);
            Assert.AreEqual(24, outTex.height);
        }

        // ---------------------------------------------------------------
        // 2. グレースケール化
        // ---------------------------------------------------------------
        [Test]
        public void Grayscale()
        {
            var tex = CreateTexture(16, 16, (x, y) => new Color32(255, 128, 64, 255));
            using var mat = Unity.TextureToMat(tex);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            Assert.AreEqual(1, gray.Channels());
            Assert.AreEqual(16, gray.Width);
        }

        // ---------------------------------------------------------------
        // 3. 2値化
        // ---------------------------------------------------------------
        [Test]
        public void Binarize()
        {
            using var mat = MakeBgr(32, 32, 100, 100, 200);
            using var gray = new Mat();
            using var bin = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 127, 255, ThresholdTypes.Binary);
            var p = bin.At<byte>(0, 0);
            Assert.IsTrue(p == 0 || p == 255);
        }

        // ---------------------------------------------------------------
        // 4. 輪郭検出
        // ---------------------------------------------------------------
        [Test]
        public void FindContours()
        {
            using var mat = MakeBgr(64, 64, 0, 0, 0);
            Cv2.Circle(mat, new Point(32, 32), 15, new Scalar(255, 255, 255), -1);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            using var bin = new Mat();
            Cv2.Threshold(gray, bin, 127, 255, ThresholdTypes.Binary);

            Cv2.FindContours(bin, out Point[][] contours, out HierarchyIndex[] hierarchy,
                RetrievalModes.External, ContourApproximationModes.ApproxNone);
            Assert.Greater(contours.Length, 0);
            double area = Cv2.ContourArea(contours[0]);
            Assert.Greater(area, 0);
            Moments m = Cv2.Moments(contours[0]);
            Assert.Greater(m.M00, 0);
            Cv2.DrawContours(mat, new[] { contours[0] }, 0, new Scalar(0, 0, 255), 8);
        }

        // ---------------------------------------------------------------
        // 5. 画像の射影変換
        // ---------------------------------------------------------------
        [Test]
        public void PerspectiveWarp()
        {
            using var mat = MakeBgr(200, 200);
            var src = new[] { new Point2f(0, 0), new Point2f(200, 0), new Point2f(200, 200), new Point2f(0, 200) };
            var dst = new[] { new Point2f(50, 25), new Point2f(150, 25), new Point2f(180, 180), new Point2f(20, 180) };
            using var t = Cv2.GetPerspectiveTransform(src, dst);
            using var outMat = new Mat();
            Cv2.WarpPerspective(mat, outMat, t, mat.Size());
            Assert.AreEqual(mat.Size(), outMat.Size());
        }

        // ---------------------------------------------------------------
        // 6. 座標の射影変換
        // ---------------------------------------------------------------
        [Test]
        public void PerspectiveTransformPoints()
        {
            var src = new[] { new Point2f(0, 0), new Point2f(100, 0), new Point2f(100, 100), new Point2f(0, 100) };
            var dst = new[] { new Point2f(0, 0), new Point2f(50, 0), new Point2f(50, 50), new Point2f(0, 50) };
            using var t = Cv2.GetPerspectiveTransform(src, dst);
            var outPos = Cv2.PerspectiveTransform(new[] { new Point2f(100, 100) }, t);
            Assert.AreEqual(1, outPos.Length);
            Assert.AreEqual(50f, outPos[0].X, 1e-3f);
            Assert.AreEqual(50f, outPos[0].Y, 1e-3f);
        }

        // ---------------------------------------------------------------
        // 7. ガウシアンブラー
        // ---------------------------------------------------------------
        [Test]
        public void GaussianBlurSample()
        {
            using var mat = MakeBgr(64, 64, 50, 100, 150);
            using var blur = new Mat();
            Cv2.GaussianBlur(mat, blur, new Size(11, 11), 0);
            Assert.AreEqual(mat.Size(), blur.Size());
            var p = blur.At<Vec3b>(32, 32);
            Assert.AreEqual(50, p[0]); // uniform image -> unchanged (B)
        }

        // ---------------------------------------------------------------
        // 8. 膨張処理
        // ---------------------------------------------------------------
        [Test]
        public void DilateSample()
        {
            using var bin = MakeGray(32, 32, 0);
            Cv2.Rectangle(bin, new Rect(12, 12, 8, 8), new Scalar(255), -1);
            using var dst = new Mat();
            using var kernel = new Mat();
            Cv2.Dilate(bin, dst, kernel, null, 3);
            // white 8x8 block [12,20) dilated ~3px -> covers ~[9,23)
            var pCenter = dst.At<byte>(16, 16);
            var pGrown = dst.At<byte>(10, 10);
            Assert.AreEqual(255, pCenter);
            Assert.AreEqual(255, pGrown); // block expanded into this point
        }

        // ---------------------------------------------------------------
        // 9. Sobelフィルタ
        // ---------------------------------------------------------------
        [Test]
        public void SobelSample()
        {
            using var gray = MakeGray(32, 32, 100);
            using var sobel = new Mat();
            Cv2.Sobel(gray, sobel, MatType.CV_8UC1, 0, 1);
            Assert.AreEqual(32, sobel.Width);
            Assert.AreEqual(1, sobel.Channels());
        }

        // ---------------------------------------------------------------
        // 10. ハイパスフィルタ (Filter2D)
        // ---------------------------------------------------------------
        [Test]
        public void HighPassFilter()
        {
            using var gray = MakeGray(32, 32, 100);
            double[] data = { -1, -1, -1, -1, 8, -1, -1, -1, -1 };
            using var kernel = Mat.FromPixelData(3, 3, MatType.CV_64FC1, data);
            using var dst = new Mat();
            Cv2.Filter2D(gray, dst, MatType.CV_8UC1, kernel);
            Assert.AreEqual(gray.Size(), dst.Size());
        }

        // ---------------------------------------------------------------
        // 11. 論理演算 (マスク)
        // ---------------------------------------------------------------
        [Test]
        public void BitwiseMask()
        {
            using var mat = MakeBgr(64, 64, 30, 30, 30);
            using var mask = new Mat(64, 64, MatType.CV_8UC3, new Scalar(0, 0, 0));
            Cv2.Circle(mask, new Point(32, 32), 10, new Scalar(255, 255, 255), -1);
            using var dst = new Mat();
            Cv2.BitwiseAnd(mat, mask, dst);
            var inside = dst.At<Vec3b>(32, 32);
            var outside = dst.At<Vec3b>(2, 2);
            Assert.AreEqual(30, inside[0]);  // inside mask -> original
            Assert.AreEqual(0, outside[0]);  // outside mask -> black
        }

        // ---------------------------------------------------------------
        // 12. レイヤ分割
        // ---------------------------------------------------------------
        [Test]
        public void SplitChannels()
        {
            using var mat = MakeBgr(16, 16, 10, 20, 30);
            Mat[] layers = Cv2.Split(mat);
            Assert.AreEqual(3, layers.Length);
            var b = layers[0].At<byte>(0, 0);
            var g = layers[1].At<byte>(0, 0);
            var r = layers[2].At<byte>(0, 0);
            Assert.AreEqual(10, b);
            Assert.AreEqual(20, g);
            Assert.AreEqual(30, r);
            foreach (var l in layers) l.Dispose();
        }

        // ---------------------------------------------------------------
        // 13. 左右反転
        // ---------------------------------------------------------------
        [Test]
        public void FlipSample()
        {
            using var mat = MakeBgr(16, 16, 10, 10, 10);
            Cv2.Circle(mat, new Point(2, 8), 1, new Scalar(255, 255, 255), -1);
            using var dst = new Mat();
            Cv2.Flip(mat, dst, FlipMode.Y);
            // white dot at x=2 flips to x=13 (width-1-2)
            var p = dst.At<Vec3b>(8, 13);
            Assert.AreEqual(255, p[0]);
        }

        // ---------------------------------------------------------------
        // 14. 図形描画
        // ---------------------------------------------------------------
        [Test]
        public void DrawShapes()
        {
            using var mat = new Mat(512, 512, MatType.CV_8UC3, new Scalar(30, 30, 30));
            Cv2.Circle(mat, new Point(100, 100), 50, new Scalar(255, 0, 0), -1, LineTypes.AntiAlias);
            Cv2.Rectangle(mat, new OpenCvSharp.Rect(50, 180, 300, 100), new Scalar(0, 0, 255), -1, LineTypes.AntiAlias);
            var circleCenter = mat.At<Vec3b>(100, 100);
            var rectCenter = mat.At<Vec3b>(230, 230);
            Assert.AreEqual(255, circleCenter[0]);
            Assert.AreEqual(255, rectCenter[2]);
        }

        // ---------------------------------------------------------------
        // 15. Arucoマーカー作成
        // (CvAruco.DrawMarker は本ライブラリに無いため、等価な
        //   Dictionary.GenerateImageMarker を使用)
        // ---------------------------------------------------------------
        [Test]
        public void ArucoCreateMarker()
        {
            var dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
            using var mat = new Mat();
            dictionary.GenerateImageMarker(5, 150, mat);
            Assert.AreEqual(150, mat.Width);
            Assert.AreEqual(150, mat.Height);
            Assert.AreEqual(1, mat.Channels());
        }

        // ---------------------------------------------------------------
        // 16. Arucoマーカー検出
        // ---------------------------------------------------------------
        [Test]
        public void ArucoDetectMarker()
        {
            var dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
            using var marker = new Mat();
            dictionary.GenerateImageMarker(7, 100, marker);
            // embed on a white canvas so the marker has a margin (robust detection)
            using var canvas = new Mat(140, 140, MatType.CV_8UC1, new Scalar(255));
            using (var roi = new Mat(canvas, new OpenCvSharp.Rect(20, 20, 100, 100)))
                marker.CopyTo(roi);

            CvAruco.DetectMarkers(canvas, dictionary, out Point2f[][] corners, out int[] ids,
                new DetectorParameters(), out Point2f[][] rejected);
            Assert.IsNotNull(corners);
            Assert.IsNotNull(ids);
            Assert.Greater(ids.Length, 0);
            Assert.AreEqual(7, ids[0]);
        }

        // ---------------------------------------------------------------
        // 17. ピクセルアクセス (DataPointer)
        // ---------------------------------------------------------------
        [Test]
        public unsafe void PixelAccessDataPointer()
        {
            using var mat = MakeBgr(16, 16, 30, 30, 30);
            byte* data = mat.DataPointer;
            int width = mat.Width, height = mat.Height, channel = mat.Channels();
            for (int y = 0; y < height / 4; y++)
                for (int x = 0; x < width / 4; x++)
                {
                    int idx = (x + y * width) * channel;
                    data[idx] = 255;
                    data[idx + 1] = 255;
                    data[idx + 2] = 255;
                }
            var p = mat.At<Vec3b>(0, 0);
            Assert.AreEqual(255, p[0]);
            Assert.AreEqual(255, p[2]);
        }

        // ---------------------------------------------------------------
        // 18. 顔検出 (カスケード分類器)
        // ---------------------------------------------------------------
        [Test]
        public void FaceDetect()
        {
            string cascadePath = "Assets/Tests/Data/haarcascade_frontalface_default.xml";
            using var cascade = new CascadeClassifier(cascadePath);
            Assert.IsFalse(cascade.Empty());
            using var gray = MakeGray(64, 64, 128);
            OpenCvSharp.Rect[] faces = cascade.DetectMultiScale(gray);
            // blank image -> no faces, but must not throw
            Assert.IsNotNull(faces);
            Assert.AreEqual(0, faces.Length);
        }

        // ---------------------------------------------------------------
        // 19. ROI トリミング
        // ---------------------------------------------------------------
        [Test]
        public void RoiCrop()
        {
            using var mat = MakeBgr(64, 64, 30, 30, 30);
            var rect = new OpenCvSharp.Rect(10, 10, 30, 30);
            using var roi = new Mat(mat, rect);
            Assert.AreEqual(30, roi.Width);
            Assert.AreEqual(30, roi.Height);
        }
    }
}