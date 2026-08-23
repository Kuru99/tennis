using System;
using System.IO;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrideCourt.Editor
{
    public static class TennisGeneratedModelIntegrator
    {
        public const string LuxModelPath = "Assets/_Project/Art/Models/Generated/Lux_Fox_Model_v1.obj";
        public const string BastionModelPath = "Assets/_Project/Art/Models/Generated/Bastion_Robot_Model_v1.obj";
        public const string LuxRiggedPrefabPath = "Assets/_Project/Generated/RiggedModels/Lux_Rigged.prefab";
        public const string BastionRiggedPrefabPath = "Assets/_Project/Generated/RiggedModels/Bastion_Rigged.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/MVP_Prototype.unity";
        private static readonly Vector3 ModelLocalPosition = new Vector3(0f, -1f, 0f);
        private static readonly Vector3 ModelLocalScale = new Vector3(0.9f, 0.72f, 0.9f);

        [MenuItem("Pride Court/Integrate Generated Character Models")]
        public static void IntegrateGeneratedModels()
        {
            ConfigureModelImporter(LuxModelPath);
            ConfigureModelImporter(BastionModelPath);
            RequireModel(LuxModelPath);
            RequireModel(BastionModelPath);
            BuildRiggedPrefabs();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            TennisAthleteController[] athletes = UnityEngine.Object.FindObjectsByType<TennisAthleteController>(FindObjectsInactive.Exclude);
            if (athletes.Length != 2)
            {
                throw new InvalidOperationException("PRIDE_COURT_MODEL_INTEGRATION_FAILED: Exactly two athletes are required.");
            }

            foreach (TennisAthleteController athlete in athletes)
            {
                AttachGeneratedModels(athlete.gameObject, athlete.Identity);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PRIDE_COURT_GENERATED_MODELS_INTEGRATED");
        }

        private static void ConfigureModelImporter(string modelPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("PRIDE_COURT_MODEL_INTEGRATION_FAILED: Model importer is missing: " + modelPath);
            }

            // Blender OBJ exports can otherwise collapse named objects into one renderer. The
            // runtime rig needs those object boundaries to animate limbs and hold the racket.
            importer.preserveHierarchy = true;
            importer.sortHierarchyByName = true;
            importer.SaveAndReimport();
        }

        public static void IntegrateFromCommandLine()
        {
            IntegrateGeneratedModels();
        }

        public static void CaptureScenePreviewFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                throw new InvalidOperationException("PRIDE_COURT_MODEL_PREVIEW_FAILED: Main camera is missing.");
            }

            camera.transform.position = TennisCameraController.DefaultBasePosition;
            camera.transform.rotation = Quaternion.LookRotation(
                TennisCameraController.DefaultLookTarget - camera.transform.position,
                Vector3.up);

            const int width = 1280;
            const int height = 720;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                string outputPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    "Logs", "GeneratedModelScenePreview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Debug.Log("PRIDE_COURT_MODEL_PREVIEW_CAPTURED: " + outputPath);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        public static void AttachGeneratedModels(GameObject athleteObject, AthleteIdentity activeIdentity)
        {
            if (athleteObject == null)
            {
                throw new ArgumentNullException(nameof(athleteObject));
            }

            Renderer rootRenderer = athleteObject.GetComponent<Renderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            DestroyDirectChild(athleteObject.transform, "Prototype Racket");
            DestroyDirectChild(athleteObject.transform, "Lux Silhouette");
            DestroyDirectChild(athleteObject.transform, "Bastion Silhouette");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LuxRiggedPrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(BastionRiggedPrefabPath) == null)
            {
                BuildRiggedPrefabs();
            }

            GameObject luxVisual = InstantiateModel(LuxRiggedPrefabPath, "Lux Silhouette", athleteObject.transform);
            GameObject bastionVisual = InstantiateModel(BastionRiggedPrefabPath, "Bastion Silhouette", athleteObject.transform);
            luxVisual.SetActive(activeIdentity == AthleteIdentity.Lux);
            bastionVisual.SetActive(activeIdentity == AthleteIdentity.Bastion);
        }

        private static GameObject InstantiateModel(string assetPath, string instanceName, Transform parent)
        {
            GameObject modelAsset = RequireModel(assetPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("PRIDE_COURT_MODEL_INTEGRATION_FAILED: Could not instantiate " + assetPath);
            }

            instance.name = instanceName;
            instance.transform.localPosition = ModelLocalPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = ModelLocalScale;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return instance;
        }

        private static void BuildRiggedPrefabs()
        {
            EnsureFolder("Assets/_Project/Generated/RiggedModels");
            BuildRiggedPrefab(LuxModelPath, LuxRiggedPrefabPath, AthleteIdentity.Lux);
            BuildRiggedPrefab(BastionModelPath, BastionRiggedPrefabPath, AthleteIdentity.Bastion);
            AssetDatabase.SaveAssets();
        }

        private static void BuildRiggedPrefab(string modelPath, string prefabPath, AthleteIdentity identity)
        {
            GameObject source = RequireModel(modelPath);
            // Model-prefab instances can keep an importer-owned connection while their parts are
            // reparented into the runtime rig. A disconnected clone guarantees every mesh is
            // saved into the generated prefab instead of being dropped on a later regeneration.
            GameObject working = UnityEngine.Object.Instantiate(source);
            if (working == null)
            {
                throw new InvalidOperationException("PRIDE_COURT_RIG_BUILD_FAILED: Could not instantiate " + modelPath);
            }

            try
            {
                int sourceRendererCount = working.GetComponentsInChildren<Renderer>(true).Length;
                if (sourceRendererCount == 0)
                {
                    throw new InvalidOperationException("PRIDE_COURT_RIG_BUILD_FAILED: Model has no renderers: " + modelPath);
                }
                working.name = identity == AthleteIdentity.Lux ? "Lux Rigged Model" : "Bastion Rigged Model";
                foreach (Collider collider in working.GetComponentsInChildren<Collider>(true))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
                GeneratedCharacterRig rig = working.GetComponent<GeneratedCharacterRig>();
                if (rig == null) rig = working.AddComponent<GeneratedCharacterRig>();
                rig.BuildRig(identity);
                if (PrefabUtility.SaveAsPrefabAsset(working, prefabPath) == null)
                {
                    throw new InvalidOperationException("PRIDE_COURT_RIG_BUILD_FAILED: Could not save " + prefabPath);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(working);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static GameObject RequireModel(string assetPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                throw new InvalidOperationException("PRIDE_COURT_MODEL_INTEGRATION_FAILED: Model was not imported: " + assetPath);
            }

            return model;
        }

        private static void DestroyDirectChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
