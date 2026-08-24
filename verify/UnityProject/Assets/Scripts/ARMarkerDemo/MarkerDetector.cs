using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using UnityEngine;

namespace PaperPlaneTools.AR
{
    /// <summary>
    /// Detects ArUco markers (Dict6X6_250) and estimates their pose (SolvePnP)
    /// with a simple pinhole camera model derived from the image resolution.
    /// </summary>
    public class MarkerDetector
    {
        private readonly List<Matrix4x4> markerTransforms = new List<Matrix4x4>();
        private readonly Dictionary dictionary;

        /// <summary>Marker side length in meters used for pose estimation.</summary>
        public float MarkerSizeInMeters = 1f;

        public MarkerDetector()
        {
            dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
        }

        /// <summary>
        /// Detects markers in the given image.
        /// Returns the list of detected marker ids, ordered by index; the pose for
        /// index i can be retrieved via <see cref="TransfromMatrixForIndex"/>.
        /// </summary>
        public List<int> Detect(Mat mat, int width, int height)
        {
            List<int> result = new List<int>();
            markerTransforms.Clear();

            DetectorParameters detectorParameters = new DetectorParameters();

            Point2f[][] corners;
            int[] ids;
            Point2f[][] rejectedImgPoints;

            Mat grayMat = new Mat();
            Cv2.CvtColor(mat, grayMat, ColorConversionCodes.BGR2GRAY);
            CvAruco.DetectMarkers(grayMat, dictionary, out corners, out ids, detectorParameters, out rejectedImgPoints);
            grayMat.Dispose();

            if (ids == null || ids.Length == 0)
                return result;

            Point3f[] markerPoints = new Point3f[]
            {
                new Point3f(-MarkerSizeInMeters / 2f,  MarkerSizeInMeters / 2f, 0f),
                new Point3f( MarkerSizeInMeters / 2f,  MarkerSizeInMeters / 2f, 0f),
                new Point3f( MarkerSizeInMeters / 2f, -MarkerSizeInMeters / 2f, 0f),
                new Point3f(-MarkerSizeInMeters / 2f, -MarkerSizeInMeters / 2f, 0f)
            };

            double maxWh = Math.Max(width, height);
            double fx = maxWh;
            double fy = maxWh;
            double cx = width / 2.0;
            double cy = height / 2.0;
            double[,] cameraMatrix = new double[3, 3]
            {
                { fx, 0d, cx },
                { 0d, fy, cy },
                { 0d, 0d, 1d }
            };
            double[] distCoeffs = new double[] { 0d, 0d, 0d, 0d };

            double[] rvec = new double[3];
            double[] tvec = new double[3];
            double[,] rotMat;

            for (int i = 0; i < ids.Length; i++)
            {
                Cv2.SolvePnP(markerPoints, corners[i], cameraMatrix, distCoeffs,
                    ref rvec, ref tvec, false, SolvePnPFlags.Iterative);

                Cv2.Rodrigues(rvec, out rotMat, out _);

                Matrix4x4 matrix = new Matrix4x4();
                matrix.SetRow(0, new Vector4((float)rotMat[0, 0], (float)rotMat[0, 1], (float)rotMat[0, 2], (float)tvec[0]));
                matrix.SetRow(1, new Vector4((float)rotMat[1, 0], (float)rotMat[1, 1], (float)rotMat[1, 2], (float)tvec[1]));
                matrix.SetRow(2, new Vector4((float)rotMat[2, 0], (float)rotMat[2, 1], (float)rotMat[2, 2], (float)tvec[2]));
                matrix.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

                result.Add(ids[i]);
                markerTransforms.Add(matrix);
            }

            return result;
        }

        /// <summary>Returns the transform matrix for a previously detected marker.</summary>
        public Matrix4x4 TransfromMatrixForIndex(int markerIndex)
        {
            return markerTransforms[markerIndex];
        }
    }
}
