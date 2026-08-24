using System;
using OpenCvSharp;
using UnityEngine;

namespace PaperPlaneTools.AR
{
    /// <summary>
    /// Base WebCamera class that takes care about video capturing.
    /// Is intended to be sub-classed and partially overridden to get
    /// desired behavior in the user Unity script.
    /// Ported to this project's OpenCvSharp.Unity helper API.
    /// </summary>
    public abstract class WebCamera : MonoBehaviour
    {
        public GameObject Surface;

        protected WebCamDevice? webCamDevice = null;
        protected WebCamTexture webCamTexture = null;
        protected Texture2D renderedTexture = null;

        /// <summary>Workaround for macOS: MacBook doesn't state its webcam as frontal.</summary>
        protected bool forceFrontalCamera = false;
        protected bool preferRearCamera = false;

        public string DeviceName
        {
            get { return webCamDevice.HasValue ? webCamDevice.Value.name : null; }
            set
            {
                if (value == DeviceName)
                    return;

                if (webCamTexture != null && webCamTexture.isPlaying)
                    webCamTexture.Stop();

                int cameraIndex = -1;
                for (int i = 0; i < WebCamTexture.devices.Length && cameraIndex == -1; i++)
                {
                    if (WebCamTexture.devices[i].name == value)
                        cameraIndex = i;
                }

                if (cameraIndex != -1)
                {
                    webCamDevice = WebCamTexture.devices[cameraIndex];
                    webCamTexture = new WebCamTexture(webCamDevice.Value.name);
                    webCamTexture.Play();
                }
                else
                {
                    throw new ArgumentException(
                        string.Format("{0}: provided DeviceName is not a correct device identifier", GetType().Name));
                }
            }
        }

        protected virtual void Awake()
        {
            if (WebCamTexture.devices.Length > 0)
                DeviceName = WebCamTexture.devices[WebCamTexture.devices.Length - 1].name;
        }

        protected virtual void OnDestroy()
        {
            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                    webCamTexture.Stop();
                webCamTexture = null;
            }
            webCamDevice = null;
        }

        private void Update()
        {
            if (webCamTexture != null && webCamTexture.didUpdateThisFrame)
            {
                if (ProcessTexture(webCamTexture, ref renderedTexture))
                    RenderFrame();
            }
        }

        /// <summary>Processes current texture; sub-classes override this.</summary>
        protected abstract bool ProcessTexture(WebCamTexture input, ref Texture2D output);

        /// <summary>Builds the OpenCV Mat from the webcam frame.</summary>
        protected Mat FrameToMat(WebCamTexture input, bool flipHorizontally)
        {
            return OpenCvSharp.Unity.PixelsToMat(input.GetPixels32(), input.width, input.height,
                flipVertically: false, flipHorizontally: flipHorizontally);
        }

        private void RenderFrame()
        {
            if (renderedTexture == null || Surface == null)
                return;

            var rawImage = Surface.GetComponent<UnityEngine.UI.RawImage>();
            if (rawImage != null)
            {
                rawImage.texture = renderedTexture;
                Surface.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(renderedTexture.width, renderedTexture.height);
            }
        }
    }
}
