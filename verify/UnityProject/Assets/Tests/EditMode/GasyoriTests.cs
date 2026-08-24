using System;
using NUnit.Framework;
using OpenCvSharp;
using UnityEngine;

namespace OpenCvSharp.Tests
{
    /// <summary>
    /// Ports of the "画像処理100本ノック" samples from
    /// https://github.com/Hirai0827/GaSyori100knockWithOpenCVplusUnity
    /// as EditMode tests. Each test runs a q1..q10 operation through the
    /// Unity &lt;-&gt; OpenCV texture adapter (Unity.TextureToMat / Unity.MatToTexture).
    /// </summary>
    [TestFixture]
    public class GasyoriTests
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

        private static Mat ToMat(Texture2D tex)
        {
            return Unity.TextureToMat(tex);
        }

        private static Texture2D ToTexture(Mat mat)
        {
            return Unity.MatToTexture(mat);
        }

        // ---------------------------------------------------------------
        // q1: チャンネル順の入れ替え (BGR -> RGB)
        // ---------------------------------------------------------------
[Test]
        public void q1_ChannelSwap()
        {
            // uniform RGB(10,20,30): TextureToMat -> BGR(30,20,10); BGR2RGB -> RGB(10,20,30)
            var tex = CreateTexture(16, 16, (x, y) => new Color32(10, 20, 30, 255));
            using var mat = ToMat(tex);
            using var changed = new Mat();
            Cv2.CvtColor(mat, changed, ColorConversionCodes.BGR2RGB);

            var v = changed.At<Vec3b>(0, 0);
            Assert.AreEqual(10, v[0]);  // R
            Assert.AreEqual(20, v[1]);  // G
            Assert.AreEqual(30, v[2]);  // B
        }

        // ---------------------------------------------------------------
        // q2: グレースケール化
        // ---------------------------------------------------------------
        [Test]
        public void q2_Grayscale()
        {
            var tex = CreateTexture(16, 16, (x, y) => new Color32(255, 128, 64, 255));
            using var mat = ToMat(tex);
            // BGR order after TextureToMat: v[0]=B, v[1]=G, v[2]=R
            for (int yi = 0; yi < mat.Height; yi++)
            {
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var v = mat.At<Vec3b>(yi, xi);
                    float gr = 0.2126f * v[2] + 0.7152f * v[1] + 0.0722f * v[0];
                    v[0] = (byte)gr; v[1] = (byte)gr; v[2] = (byte)gr;
                    mat.Set<Vec3b>(yi, xi, v);
                }
            }
            var p = mat.At<Vec3b>(0, 0);
            Assert.AreEqual(p[0], p[1]);
            Assert.AreEqual(p[1], p[2]);
        }

        // ---------------------------------------------------------------
        // q3: 2値化
        // ---------------------------------------------------------------
        [Test]
        public void q3_Binarize()
        {
            var tex = CreateTexture(16, 16, (x, y) => new Color32(200, 100, 50, 255));
            using var mat = ToMat(tex);
            for (int yi = 0; yi < mat.Height; yi++)
            {
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var v = mat.At<Vec3b>(yi, xi);
                    float gr = 0.2126f * v[2] + 0.7152f * v[1] + 0.0722f * v[0];
                    gr = gr < 128 ? 0 : 255;
                    v[0] = (byte)gr; v[1] = (byte)gr; v[2] = (byte)gr;
                    mat.Set<Vec3b>(yi, xi, v);
                }
            }
            var p = mat.At<Vec3b>(0, 0);
            Assert.IsTrue(p[0] == 0 || p[0] == 255);
            Assert.AreEqual(p[0], p[1]);
            Assert.AreEqual(p[1], p[2]);
        }

        // ---------------------------------------------------------------
        // q4: Otsuの2値化
        // ---------------------------------------------------------------
        [Test]
        public void q4_OtsuThreshold()
        {
            var tex = CreateTexture(32, 32, (x, y) =>
                new Color32((byte)(x < 16 ? 40 : 220), (byte)(x < 16 ? 40 : 220), (byte)(x < 16 ? 40 : 220), 255));
            using var mat = ToMat(tex);
            float[] results = new float[256];
            float[,] grs = new float[mat.Height, mat.Width];
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var v = mat.At<Vec3b>(yi, xi);
                    grs[yi, xi] = 0.2126f * v[2] + 0.7152f * v[1] + 0.0722f * v[0];
                }
            int total = mat.Height * mat.Width;
            for (int thi = 1; thi < 255; thi++)
            {
                int w0 = 0, w1 = 0;
                float m0 = 0, m1 = 0;
                foreach (float gr in grs)
                {
                    if (gr < thi) { w0++; m0 += gr; }
                    else { w1++; m1 += gr; }
                }
                float t0 = w0 == 0 ? 0 : m0 / w0;
                float t1 = w1 == 0 ? 0 : m1 / w1;
                results[thi] = ((float)w0 / total) * ((float)w1 / total) * Mathf.Pow(t0 - t1, 2);
            }
            int z = 0;
            for (int i = 1; i < 255; i++)
                if (results[i] > results[z]) z = i;

            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    byte val = grs[yi, xi] < z ? (byte)0 : (byte)255;
                    var v = new Vec3b(val, val, val);
                    mat.Set<Vec3b>(yi, xi, v);
                }
            var p0 = mat.At<Vec3b>(0, 0);
            var p1 = mat.At<Vec3b>(0, 31);
            Assert.AreEqual(0, p0[0]);     // dark side -> 0
            Assert.AreEqual(255, p1[0]);   // bright side -> 255
        }

        // ---------------------------------------------------------------
        // q5: HSV色相シフト
        // ---------------------------------------------------------------
        [Test]
        public void q5_HueShift()
        {
            var tex = CreateTexture(16, 16, (x, y) => new Color32(255, 0, 0, 255));
            using var mat = ToMat(tex);
            using var hsv = new Mat();
            using var outBgr = new Mat();
            Cv2.CvtColor(mat, hsv, ColorConversionCodes.BGR2HSV);
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var v = hsv.At<Vec3b>(yi, xi);
                    v[0] = (byte)((v[0] - 180) % 360);
                    hsv.Set<Vec3b>(yi, xi, v);
                }
            Cv2.CvtColor(hsv, outBgr, ColorConversionCodes.HSV2BGR);
            Assert.IsNotNull(outBgr);
            Assert.Greater(outBgr.Channels(), 0);
        }

        // ---------------------------------------------------------------
        // q6: 色数削減 (ReduceColor)
        // ---------------------------------------------------------------
        [Test]
        public void q6_ReduceColor()
        {
            var tex = CreateTexture(16, 16, (x, y) => new Color32(255, 150, 50, 255));
            using var mat = ToMat(tex);
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var v = mat.At<Vec3b>(yi, xi);
                    v[0] = (byte)ReduceColor(v[0]);
                    v[1] = (byte)ReduceColor(v[1]);
                    v[2] = (byte)ReduceColor(v[2]);
                    mat.Set<Vec3b>(yi, xi, v);
                }
            // R=255 -> 224, G=150 -> 160, B=50 -> 32
            var p = mat.At<Vec3b>(0, 0);
            Assert.AreEqual(224, p[2]);
            Assert.AreEqual(160, p[1]);
            Assert.AreEqual(32, p[0]);
        }

        private static float ReduceColor(float val)
        {
            if (val < 63) return 32;
            if (val < 127) return 96;
            if (val < 191) return 160;
            return 224;
        }

        // ---------------------------------------------------------------
        // q7: 平均値プーリング (8x8)
        // ---------------------------------------------------------------
        [Test]
        public void q7_AveragePooling()
        {
            var tex = CreateTexture(128, 128, (x, y) => new Color32(255, 255, 255, 255));
            using var mat = ToMat(tex);
            for (int yi = 0; yi < 16; yi++)
                for (int xi = 0; xi < 16; xi++)
                {
                    var sum = new Vector3();
                    for (int yj = 0; yj < 8; yj++)
                        for (int xj = 0; xj < 8; xj++)
                        {
                            var v = mat.At<Vec3b>(yi * 8 + yj, xi * 8 + xj);
                            sum[0] += v[0]; sum[1] += v[1]; sum[2] += v[2];
                        }
                    var ave = new Vec3b((byte)(sum[0] / 64), (byte)(sum[1] / 64), (byte)(sum[2] / 64));
                    for (int yj = 0; yj < 8; yj++)
                        for (int xj = 0; xj < 8; xj++)
                            mat.Set<Vec3b>(yi * 8 + yj, xi * 8 + xj, ave);
                }
            var p = mat.At<Vec3b>(0, 0);
            Assert.AreEqual(255, p[0]);
        }

        // ---------------------------------------------------------------
        // q8: 最大値プーリング (8x8)
        // ---------------------------------------------------------------
        [Test]
        public void q8_MaxPooling()
        {
            var tex = CreateTexture(128, 128, (x, y) => new Color32((byte)(x % 256), (byte)(y % 256), 0, 255));
            using var mat = ToMat(tex);
            for (int yi = 0; yi < 16; yi++)
                for (int xi = 0; xi < 16; xi++)
                {
                    var max = new Vec3b();
                    for (int yj = 0; yj < 8; yj++)
                        for (int xj = 0; xj < 8; xj++)
                        {
                            var v = mat.At<Vec3b>(yi * 8 + yj, xi * 8 + xj);
                            if (max[0] < v[0]) max[0] = v[0];
                            if (max[1] < v[1]) max[1] = v[1];
                            if (max[2] < v[2]) max[2] = v[2];
                        }
                    for (int yj = 0; yj < 8; yj++)
                        for (int xj = 0; xj < 8; xj++)
                            mat.Set<Vec3b>(yi * 8 + yj, xi * 8 + xj, max);
                }
            // block(0,0) after vertical flip covers x in [0,7] -> R max = 7
            // (x is not flipped; R is channel 2 in BGR order)
            var p = mat.At<Vec3b>(0, 0);
            Assert.AreEqual(7, p[2]);
        }

        // ---------------------------------------------------------------
        // q9: ガウシアンフィルタ (3x3)
        // ---------------------------------------------------------------
        [Test]
        public void q9_GaussianFilter()
        {
            var tex = CreateTexture(16, 16, (x, y) => new Color32(100, 150, 200, 255));
            using var mat = ToMat(tex);
            var v = new Vector3[mat.Height, mat.Width];
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var vyx = mat.At<Vec3b>(yi, xi);
                    v[yi, xi][0] = vyx[0]; v[yi, xi][1] = vyx[1]; v[yi, xi][2] = vyx[2];
                }
            v = Gaussian(v, mat.Height, mat.Width);
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var vyx = new Vec3b((byte)v[yi, xi][0], (byte)v[yi, xi][1], (byte)v[yi, xi][2]);
                    mat.Set<Vec3b>(yi, xi, vyx);
                }
            var p = mat.At<Vec3b>(8, 8);
            // uniform BGR(200,150,100) unchanged by Gaussian
            Assert.AreEqual(200, p[0]); // B
            Assert.AreEqual(150, p[1]); // G
            Assert.AreEqual(100, p[2]); // R
        }

        private static Vector3[,] Gaussian(Vector3[,] target, int height, int width)
        {
            var result = target;
            for (int yi = 0; yi < height; yi++)
                for (int xi = 0; xi < width; xi++)
                {
                    var sumColor = new Vector3();
                    int[,] kernel = { { 1, 2, 1 }, { 2, 4, 2 }, { 1, 2, 1 } };
                    if (xi == 0) { kernel[0, 0] = 0; kernel[1, 0] = 0; kernel[2, 0] = 0; }
                    else if (xi == width - 1) { kernel[0, 2] = 0; kernel[1, 2] = 0; kernel[2, 2] = 0; }
                    if (yi == 0) { kernel[0, 0] = 0; kernel[0, 1] = 0; kernel[0, 2] = 0; }
                    else if (yi == height - 1) { kernel[2, 0] = 0; kernel[2, 1] = 0; kernel[2, 2] = 0; }

                    int sum = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int yy = yi + dy, xx = xi + dx;
                            if (yy < 0 || yy >= height || xx < 0 || xx >= width) continue;
                            var k = kernel[dy + 1, dx + 1];
                            sum += k;
                            sumColor[0] += target[yy, xx][0] * k;
                            sumColor[1] += target[yy, xx][1] * k;
                            sumColor[2] += target[yy, xx][2] * k;
                        }
                    if (sum > 0)
                    {
                        result[yi, xi][0] = sumColor[0] / sum;
                        result[yi, xi][1] = sumColor[1] / sum;
                        result[yi, xi][2] = sumColor[2] / sum;
                    }
                }
            return result;
        }

        // ---------------------------------------------------------------
        // q10: メディアンフィルタ (3x3)
        // ---------------------------------------------------------------
        [Test]
        public void q10_MedianFilter()
        {
            var tex = CreateTexture(16, 16, (x, y) =>
                new Color32((byte)((x + y) % 2 == 0 ? 200 : 20), 128, 64, 255));
            using var mat = ToMat(tex);
            var v = new Vector3[mat.Height, mat.Width];
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var vyx = mat.At<Vec3b>(yi, xi);
                    v[yi, xi][0] = vyx[0]; v[yi, xi][1] = vyx[1]; v[yi, xi][2] = vyx[2];
                }
            v = Median(v, mat.Height, mat.Width);
            for (int yi = 0; yi < mat.Height; yi++)
                for (int xi = 0; xi < mat.Width; xi++)
                {
                    var vyx = new Vec3b((byte)v[yi, xi][0], (byte)v[yi, xi][1], (byte)v[yi, xi][2]);
                    mat.Set<Vec3b>(yi, xi, vyx);
                }
            var p = mat.At<Vec3b>(8, 8);
            // BGR order: B=64, G=128, R=median of checker pattern (20 or 200)
            Assert.AreEqual(64, p[0]);
            Assert.AreEqual(128, p[1]);
            Assert.IsTrue(p[2] == 20 || p[2] == 200);
        }

        private static Vector3[,] Median(Vector3[,] target, int height, int width)
        {
            var result = target;
            var neighbors = new float[9];
            for (int yi = 0; yi < height; yi++)
                for (int xi = 0; xi < width; xi++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int yy = yi + dy, xx = xi + dx;
                                if (yy < 0 || yy >= height || xx < 0 || xx >= width) continue;
                                neighbors[n++] = target[yy, xx][c];
                            }
                        Array.Sort(neighbors, 0, n);
                        result[yi, xi][c] = neighbors[n / 2];
                    }
                }
            return result;
        }
    }
}