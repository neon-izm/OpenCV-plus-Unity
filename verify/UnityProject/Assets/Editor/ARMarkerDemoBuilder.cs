using System.IO;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PaperPlaneTools.AR;

namespace ARMarkerDemo.EditorTools
{
    /// <summary>
    /// Editor utilities to build / verify the AR marker demo.
    ///  - BuildARMarkerDemoScene: creates & saves Assets/Scenes/ARMarkerDemo.unity
    ///  - GenerateMarkerAsset: writes marker id 0 (Dict6X6_250) PNG to Assets/Markers
    /// </summary>
    public static class ARMarkerDemoBuilder
    {
        private const string ScenePath = "Assets/Scenes/ARMarkerDemo.unity";
        private const string MarkerDir = "Assets/Markers";

        [MenuItem("OpenCvVerify/Build AR Marker Demo Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraGo = new GameObject("AR Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            cameraGo.AddComponent<AudioListener>();

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject surfaceGo = new GameObject("WebCameraSurface");
            surfaceGo.AddComponent<CanvasRenderer>();
            RawImage rawImage = surfaceGo.AddComponent<RawImage>();
            rawImage.color = Color.white;
            RectTransform surfaceRect = surfaceGo.GetComponent<RectTransform>();
            surfaceRect.sizeDelta = new Vector2(640, 480);

            // ScreenSpaceCamera: the webcam preview is drawn at the canvas plane and
            // 3D AR objects between the camera and that plane appear in FRONT of it.
            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvasGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            canvasGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            surfaceGo.transform.SetParent(canvasGo.transform, false);
            surfaceRect.anchorMin = new Vector2(0.5f, 0.5f);
            surfaceRect.anchorMax = new Vector2(0.5f, 0.5f);
            surfaceRect.pivot = new Vector2(0.5f, 0.5f);

            GameObject markerPrefab = CreateMarkerPrefab();

            GameObject mainGo = new GameObject("MainScript");
            MainScript mainScript = mainGo.AddComponent<MainScript>();
            //mainScript.Surface = surfaceGo;
            mainScript.markers = new System.Collections.Generic.List<MainScript.MarkerObject>
            {
                new MainScript.MarkerObject { markerId = 0, markerPrefab = markerPrefab }
            };

            GameObject scalerGo = new GameObject("SceneScaler");
            SceneScaler scaler = scalerGo.AddComponent<SceneScaler>();
            scaler.arCamera = camera;
            scaler.outputSurface = surfaceGo;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ARMarkerDemoBuilder] Scene saved to {ScenePath}");
        }

        private static GameObject CreateMarkerPrefab()
        {
            GameObject go = new GameObject("MarkerObject");
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Standard"));
            go.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            string dir = "Assets/Prefabs";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/MarkerObject.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[ARMarkerDemoBuilder] Marker prefab saved to {path}");
            return prefab;
        }

        [MenuItem("OpenCvVerify/Generate Marker PNG (id 0)")]
        public static void GenerateMarkerAsset()
        {
            Directory.CreateDirectory(MarkerDir);
            using (Dictionary dictionary =
                CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250))
            using (Mat marker = new Mat())
            {
                dictionary.GenerateImageMarker(0, 400, marker, 1);
                string path = $"{MarkerDir}/marker_0.png";
                if (Cv2.ImWrite(path, marker))
                    Debug.Log($"[ARMarkerDemoBuilder] Marker written to {path}");
                else
                    Debug.LogError($"[ARMarkerDemoBuilder] Failed to write {path}");
            }
        }
    }
}
