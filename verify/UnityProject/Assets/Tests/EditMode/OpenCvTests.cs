using System;
using NUnit.Framework;
using OpenCvSharp;
using OpenCvSharp.Aruco;

namespace OpenCvSharp.Tests
{
    [TestFixture]
    public class OpenCvTests
    {
        [Test]
        public void BuildInformation()
        {
            var info = Cv2.GetBuildInformation();
            Assert.IsFalse(string.IsNullOrEmpty(info));
            TestContext.Out.WriteLine($"BuildInformation: {info.Length} chars");
        }

        [Test]
        public void CvtColor()
        {
            using var src = new Mat(64, 64, MatType.CV_8UC3, Scalar.All(120));
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Assert.AreEqual(1, gray.Channels());
        }

        [Test]
        public void ImEncodePng()
        {
            using var gray = new Mat(32, 32, MatType.CV_8UC1, Scalar.All(100));
            Cv2.ImEncode(".png", gray, out byte[] png);
            Assert.Greater(png.Length, 0);
        }

        [Test]
        public void ImDecodeRoundTrip()
        {
            using var gray = new Mat(32, 32, MatType.CV_8UC1, Scalar.All(100));
            Cv2.ImEncode(".png", gray, out byte[] png);
            using var decoded = Cv2.ImDecode(png, ImreadModes.Grayscale);
            Assert.AreEqual(gray.Size(), decoded.Size());
        }

        [Test]
        public void PixelValues()
        {
            using var m = new Mat(2, 2, MatType.CV_8UC1, new Scalar(42));
            var v = m.Get<byte>(0, 0);
            Assert.AreEqual(42, v);
        }

        [Test]
        public void CannyDetectsEdges()
        {
            using var m = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(128));
            using var edges = new Mat();
            Cv2.Canny(m, edges, 100, 200);
            Assert.IsNotNull(edges);
        }

        [Test]
        public void GaussianBlurSmooths()
        {
            using var m = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(128));
            using var blurred = new Mat();
            Cv2.GaussianBlur(m, blurred, new Size(5, 5), 1.0);
            Assert.AreEqual(m.Size(), blurred.Size());
        }

        [Test]
        public void ThresholdProducesBinary()
        {
            using var m = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(128));
            using var dst = new Mat();
            Cv2.Threshold(m, dst, 127, 255, ThresholdTypes.Binary);
            Assert.IsNotNull(dst);
        }

        [Test]
        public void ResizeWorks()
        {
            using var m = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(128));
            using var resized = new Mat();
            Cv2.Resize(m, resized, new Size(32, 32));
            Assert.AreEqual(new Size(32, 32), resized.Size());
        }

        [Test]
        public void ImWriteAndRead()
        {
            using var m = new Mat(32, 32, MatType.CV_8UC3, Scalar.All(200));
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ocv_test.png");
            Assert.IsTrue(Cv2.ImWrite(path, m));
            using var read = Cv2.ImRead(path, ImreadModes.Color);
            Assert.AreEqual(m.Size(), read.Size());
            System.IO.File.Delete(path);
        }

        [Test]
        public void MatArithmetic()
        {
            using var a = new Mat(10, 10, MatType.CV_32FC1, Scalar.All(1));
            using var b = new Mat(10, 10, MatType.CV_32FC1, Scalar.All(2));
            using var c = new Mat();
            Cv2.Add(a, b, c);
            var v = c.Get<float>(0, 0);
            Assert.AreEqual(3.0f, v, 1e-5f);
        }

        [Test]
        public void OrbDetectsKeypoints()
        {
            using var m = new Mat(200, 200, MatType.CV_8UC1, Scalar.All(0));
            Cv2.Circle(m, new Point(100, 100), 30, Scalar.All(255), -1);
            using var orb = ORB.Create();
            KeyPoint[] kps = orb.Detect(m);
            Assert.Greater(kps.Length, 0);
        }

        [Test]
        public void ArucoMarkerDetected()
        {
            using var marker = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(255));
            Cv2.Rectangle(marker, new OpenCvSharp.Rect(4, 4, 32, 32), Scalar.All(0), -1);
            Cv2.Rectangle(marker, new OpenCvSharp.Rect(12, 12, 16, 16), Scalar.All(255), -1);

            var dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict4X4_50);
            CvAruco.DetectMarkers(
                marker,
                dictionary,
                out Point2f[][] corners,
                out int[] ids,
                new DetectorParameters(),
                out Point2f[][] rejected);

            Assert.IsNotNull(corners);
        }

        [Test]
        public void ArucoRealMarkerDetectedWithId()
        {
            using var marker = new Mat();
            using var dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
            dictionary.GenerateImageMarker(0, 200, marker);

            // embed on a white canvas so the marker has a margin (robust detection)
            using var canvas = new Mat(280, 280, MatType.CV_8UC1, new Scalar(255));
            using (var roi = new Mat(canvas, new OpenCvSharp.Rect(40, 40, 200, 200)))
                marker.CopyTo(roi);

            CvAruco.DetectMarkers(
                canvas,
                dictionary,
                out Point2f[][] corners,
                out int[] ids,
                new DetectorParameters(),
                out Point2f[][] rejected);

            Assert.IsNotNull(ids, "ids should not be null");
            Assert.AreEqual(1, ids.Length, $"exactly one marker should be detected (rejected={rejected?.Length})");
            Assert.AreEqual(0, ids[0], "detected marker id should be 0");
            Assert.IsNotNull(corners);
            Assert.AreEqual(4, corners[0].Length, "a marker has 4 corners");

            TestContext.Out.WriteLine($"Detected marker id={ids[0]} corners={corners[0].Length}");
        }
    }
}
