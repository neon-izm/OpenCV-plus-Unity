using System;
using System.IO;
using UnityEngine;
using OpenCvSharp;

public static class OpenCvVerify
{
    public static void Run()
    {
        try
        {
            string info = Cv2.GetBuildInformation();
            Debug.Log("[OpenCvVerify] BuildInformation head: " + InfoHead(info));

            Mat src = new Mat(64, 64, MatType.CV_8UC3, Scalar.All(120));
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Debug.Log("[OpenCvVerify] CvtColor OK: gray type=" + gray.Type() + " channels=" + gray.Channels());

            Cv2.ImEncode(".png", gray, out byte[] png);
            Debug.Log("[OpenCvVerify] ImEncode OK: png bytes=" + png.Length);

            Mat red = src.Clone();
            Cv2.Circle(red, new Point(32, 32), 10, Scalar.Red, -1);
            OpenCvSharp.Rect r = Cv2.BoundingRect(new Point[] { new Point(0, 0), new Point(64, 64) });
            Debug.Log("[OpenCvVerify] Circle/BoundingRect OK: rect=" + r.Width + "x" + r.Height);

            Debug.Log("[OpenCvVerify] SUCCESS");
        }
        catch (Exception e)
        {
            Debug.LogError("[OpenCvVerify] FAILED: " + e.GetType().Name + ": " + e.Message);
        }
    }

    private static string InfoHead(string info)
    {
        if (string.IsNullOrEmpty(info)) return "(empty)";
        return info.Substring(0, Math.Min(160, info.Length)).Replace("\n", " | ");
    }
}