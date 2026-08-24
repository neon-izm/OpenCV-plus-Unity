using OpenCvSharp;
using UnityEngine;

namespace PaperPlaneTools.AR
{
    /// <summary>
    /// Scales the RawImage surface to match the screen and adjusts the AR camera
    /// FOV to match the pinhole camera model used for pose estimation.
    /// </summary>
    public class SceneScaler : MonoBehaviour
    {
        public Camera arCamera;
        public GameObject outputSurface;

        private Vector2 screenSize = Vector2.zero;
        private Vector2 componentSize = Vector2.zero;

        private void Start()
        {
            screenSize = Vector2.zero;
            componentSize = Vector2.zero;
            Update();
        }

        private void Update()
        {
            Vector2 currentSize = outputSurface != null
                ? outputSurface.GetComponent<RectTransform>().sizeDelta
                : Vector2.zero;

            if (Screen.width != (int)screenSize.x || Screen.height != (int)screenSize.y ||
                currentSize.x != componentSize.x || currentSize.y != componentSize.y)
            {
                screenSize = new Vector2(Screen.width, Screen.height);
                componentSize = currentSize;
                Scale();
            }
        }

        private void Scale()
        {
            float width = componentSize.x;
            float height = componentSize.y;
            if (width <= 0 || height <= 0 || Screen.width <= 0 || Screen.height <= 0 || outputSurface == null)
                return;

            float aspectWidth = (float)Screen.width / width;
            float aspectHeight = (float)Screen.height / height;
            Size imageSize;
            float aspect;
            if (aspectWidth < aspectHeight)
            {
                aspect = aspectHeight;
                imageSize = new Size(width, height);
            }
            else
            {
                float k = (float)Screen.height / (float)Screen.width;
                imageSize = new Size(width, width * k);
                aspect = aspectWidth;
            }

            outputSurface.transform.localScale = new Vector3(aspect, aspect, 1.0f);
            AdjustFOV(imageSize);
        }

        private void AdjustFOV(Size size)
        {
            if (arCamera == null)
                return;

            float width = componentSize.x;
            float height = componentSize.y;
            double maxWh = Mathf.Max(width, height);
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

            Cv2.CalibrationMatrixValues(cameraMatrix, size, 0d, 0d,
                out double fovx, out double fovy, out double focalLength,
                out Point2d principalPoint, out double aspectratio);

            arCamera.fieldOfView = (float)fovy;
        }
    }
}
