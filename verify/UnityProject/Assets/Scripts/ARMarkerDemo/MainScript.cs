using System;
using System.Collections.Generic;
using OpenCvSharp;
using UnityEngine;

namespace PaperPlaneTools.AR
{
    /// <summary>
    /// Main AR marker demo controller. Inherits from <see cref="WebCamera"/> so it
    /// reads the device camera, detects ArUco markers and positions a virtual object
    /// on top of each detected marker.
    /// </summary>
    public class MainScript : WebCamera
    {
        [Serializable]
        public class MarkerObject
        {
            public int markerId;
            public GameObject markerPrefab;
        }

        public class MarkerOnScene
        {
            public int bestMatchIndex = -1;
            public float destroyAt = -1f;
            public GameObject gameObject;
        }

        /// <summary>List of possible markers, set in the Inspector.</summary>
        public List<MarkerObject> markers;

        private MarkerDetector markerDetector;

        private readonly Dictionary<int, List<MarkerOnScene>> gameObjects =
            new Dictionary<int, List<MarkerOnScene>>();

        protected override void Awake()
        {
            int cameraIndex = -1;
            for (int i = 0; i < WebCamTexture.devices.Length; i++)
            {
                WebCamDevice device = WebCamTexture.devices[i];
                if (!device.isFrontFacing)
                {
                    cameraIndex = i;
                    break;
                }
                if (cameraIndex < 0)
                    cameraIndex = i;
            }

            if (cameraIndex >= 0)
                DeviceName = WebCamTexture.devices[cameraIndex].name;
        }

        private void Start()
        {
            markerDetector = new MarkerDetector();
            foreach (MarkerObject markerObject in markers)
                gameObjects[markerObject.markerId] = new List<MarkerOnScene>();
        }

        protected override bool ProcessTexture(WebCamTexture input, ref Texture2D output)
        {
            using (Mat img = FrameToMat(input, forceFrontalCamera))
            {
                ProcessFrame(img, img.Cols, img.Rows);
                output = OpenCvSharp.Unity.MatToTexture(img, output);
            }
            return true;
        }

        private void ProcessFrame(Mat mat, int width, int height)
        {
            List<int> markerIds = markerDetector.Detect(mat, width, height);

            foreach (MarkerObject markerObject in markers)
            {
                List<int> foundedMarkers = new List<int>();
                for (int i = 0; i < markerIds.Count; i++)
                {
                    if (markerIds[i] == markerObject.markerId)
                        foundedMarkers.Add(i);
                }
                ProcessMarkersWithSameId(markerObject, gameObjects[markerObject.markerId], foundedMarkers);
            }
        }

        private void ProcessMarkersWithSameId(
            MarkerObject markerObject, List<MarkerOnScene> gameObjects, List<int> foundedMarkers)
        {
            int index = gameObjects.Count - 1;
            while (index >= 0)
            {
                MarkerOnScene markerOnScene = gameObjects[index];
                markerOnScene.bestMatchIndex = -1;
                if (markerOnScene.destroyAt > 0 && markerOnScene.destroyAt < Time.fixedTime)
                {
                    Destroy(markerOnScene.gameObject);
                    gameObjects.RemoveAt(index);
                }
                --index;
            }

            index = foundedMarkers.Count - 1;

            // Match markers with existing gameObjects
            while (index >= 0)
            {
                int markerIndex = foundedMarkers[index];
                Vector3 position = MatrixHelper.GetPosition(markerDetector.TransfromMatrixForIndex(markerIndex));

                float minDistance = float.MaxValue;
                int bestMatch = -1;
                for (int i = 0; i < gameObjects.Count; i++)
                {
                    MarkerOnScene markerOnScene = gameObjects[i];
                    if (markerOnScene.bestMatchIndex >= 0)
                        continue;
                    float distance = Vector3.Distance(markerOnScene.gameObject.transform.position, position);
                    if (distance < minDistance)
                    {
                        bestMatch = i;
                        minDistance = distance;
                    }
                }

                if (bestMatch >= 0)
                {
                    gameObjects[bestMatch].bestMatchIndex = markerIndex;
                    foundedMarkers.RemoveAt(index);
                }
                --index;
            }

            // Destroy excessive objects
            index = gameObjects.Count - 1;
            while (index >= 0)
            {
                MarkerOnScene markerOnScene = gameObjects[index];
                if (markerOnScene.bestMatchIndex < 0)
                {
                    if (markerOnScene.destroyAt < 0)
                        markerOnScene.destroyAt = Time.fixedTime + 0.2f;
                }
                else
                {
                    markerOnScene.destroyAt = -1f;
                    int markerIndex = markerOnScene.bestMatchIndex;
                    Matrix4x4 transformMatrix = markerDetector.TransfromMatrixForIndex(markerIndex);
                    PositionObject(markerOnScene.gameObject, transformMatrix);
                }
                index--;
            }

            // Create objects for markers not matched with any game object
            foreach (int markerIndex in foundedMarkers)
            {
                GameObject gameObject = Instantiate(markerObject.markerPrefab);
                MarkerOnScene markerOnScene = new MarkerOnScene { gameObject = gameObject };
                gameObjects.Add(markerOnScene);

                Matrix4x4 transformMatrix = markerDetector.TransfromMatrixForIndex(markerIndex);
                PositionObject(markerOnScene.gameObject, transformMatrix);
            }
        }

        private void PositionObject(GameObject gameObject, Matrix4x4 transformMatrix)
        {
            Matrix4x4 matrixY = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, -1, 1));
            Matrix4x4 matrixZ = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
            Matrix4x4 matrix = matrixY * transformMatrix * matrixZ;

            gameObject.transform.localPosition = MatrixHelper.GetPosition(matrix);
            gameObject.transform.localRotation = MatrixHelper.GetQuaternion(matrix);
            gameObject.transform.localScale = MatrixHelper.GetScale(matrix);
        }
    }
}
