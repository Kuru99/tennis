using PrideCourt.Gameplay;
using PrideCourt.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrideCourt.Editor
{
    public static class TennisStadiumEnvironmentBuilder
    {
        public const string RootName = "Pop Punk Fantasy Stadium";
        private const string ScenePath = "Assets/_Project/Scenes/MVP_Prototype.unity";
        private const string GeneratedFolder = "Assets/_Project/Generated";

        [MenuItem("Pride Court/Integrate Pop Punk Fantasy Stadium")]
        public static void IntegrateCurrentScene()
        {
            BuildOrReplace();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("PRIDE_COURT_STADIUM_INTEGRATED");
        }

        public static void IntegrateFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath);
            IntegrateCurrentScene();
            TennisPrototypeValidation.ValidateAll();
            EditorApplication.Exit(0);
        }

        public static void BuildOrReplace()
        {
            EnsureGeneratedFolder();
            DestroyOwnedObject(RootName);
            DestroyOwnedObject("Prototype Arena Boundary");

            Texture2D muralTexture = Resources.Load<Texture2D>("Stadium/FantasyStadiumMural");
            Texture2D crowdTexture = Resources.Load<Texture2D>("Stadium/CrowdMosaic");
            Texture2D bannerTexture = Resources.Load<Texture2D>("Stadium/PunkBanner");

            Material structure = CreateOrUpdateMaterial("Stadium_Structure_Navy", new Color(0.018f, 0.035f, 0.09f));
            Material cyan = CreateOrUpdateMaterial("Stadium_Electric_Cyan", new Color(0.02f, 0.78f, 1f), null, new Color(0.01f, 0.34f, 0.55f));
            Material magenta = CreateOrUpdateMaterial("Stadium_Hot_Magenta", new Color(1f, 0.02f, 0.42f), null, new Color(0.55f, 0.01f, 0.18f));
            Material yellow = CreateOrUpdateMaterial("Stadium_Acid_Yellow", new Color(1f, 0.84f, 0.02f), null, new Color(0.48f, 0.32f, 0.01f));
            Material violet = CreateOrUpdateMaterial("Stadium_Violet", new Color(0.33f, 0.08f, 0.72f), null, new Color(0.16f, 0.03f, 0.32f));
            Material mural = CreateOrUpdateMaterial("Stadium_Fantasy_Mural", Color.white, muralTexture, new Color(0.12f, 0.12f, 0.18f));
            Material crowd = CreateOrUpdateMaterial("Stadium_Crowd_Mosaic", Color.white, crowdTexture, new Color(0.08f, 0.08f, 0.12f));
            Material banner = CreateOrUpdateMaterial("Stadium_Punk_Banner", Color.white, bannerTexture, new Color(0.1f, 0.1f, 0.16f));

            GameObject root = new GameObject(RootName);
            root.transform.localScale = new Vector3(TennisCourtGeometry.CourtWidthMultiplier, 1f, 1f);
            root.AddComponent<StadiumEnvironment>();

            CreateFarBackdrop(root.transform, mural, structure);
            CreateGrandstands(root.transform, structure, violet, crowd, banner);
            CreateFantasyGate(root.transform, structure, cyan, magenta, yellow);
            CreateLightTowers(root.transform, structure, cyan, magenta, yellow);
            CreateSpeakerPods(root.transform, structure, magenta, violet);

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.008f, 0.015f, 0.055f);
            }

            RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.24f);
            Selection.activeObject = root;
        }

        private static void CreateFarBackdrop(Transform root, Material mural, Material structure)
        {
            CreateBlock("Fantasy Skyline Mural", root, new Vector3(0f, 7.4f, 25f), new Vector3(30f, 13f, 0.2f), mural);
            CreateBlock("Mural Crown", root, new Vector3(0f, 14.2f, 25.2f), new Vector3(31f, 0.35f, 0.55f), structure);
        }

        private static void CreateGrandstands(Transform root, Material structure, Material violet, Material crowd, Material banner)
        {
            GameObject stands = new GameObject("Grandstands");
            stands.transform.SetParent(root, false);

            for (int tier = 0; tier < 5; tier++)
            {
                float y = 0.3f + tier * 0.72f;
                CreateBlock("Far Stand Tier " + (tier + 1), stands.transform,
                    new Vector3(0f, y, 15.8f + tier * 1.18f), new Vector3(27f, 0.72f, 2.25f), tier % 2 == 0 ? structure : violet);

                float sideX = 7.5f + tier * 0.92f;
                CreateBlock("Left Stand Tier " + (tier + 1), stands.transform,
                    new Vector3(-sideX, y, 1.7f), new Vector3(1.75f, 0.72f, 31f), tier % 2 == 0 ? structure : violet);
                CreateBlock("Right Stand Tier " + (tier + 1), stands.transform,
                    new Vector3(sideX, y, 1.7f), new Vector3(1.75f, 0.72f, 31f), tier % 2 == 0 ? structure : violet);
            }

            CreateBlock("Far Crowd Wall", stands.transform, new Vector3(0f, 4.6f, 20.9f), new Vector3(26f, 4.6f, 0.18f), crowd);
            CreateBlock("Left Crowd Wall", stands.transform, new Vector3(-11.7f, 4.3f, 1.8f), new Vector3(0.18f, 4.8f, 31f), crowd);
            CreateBlock("Right Crowd Wall", stands.transform, new Vector3(11.7f, 4.3f, 1.8f), new Vector3(0.18f, 4.8f, 31f), crowd);

            CreateBlock("Far LED Ribbon", stands.transform, new Vector3(0f, 1.35f, 14.75f), new Vector3(26f, 1.05f, 0.22f), banner);
            CreateBlock("Left LED Ribbon", stands.transform, new Vector3(-6.85f, 1.28f, 1.5f), new Vector3(0.22f, 1f, 30f), banner);
            CreateBlock("Right LED Ribbon", stands.transform, new Vector3(6.85f, 1.28f, 1.5f), new Vector3(0.22f, 1f, 30f), banner);
        }

        private static void CreateFantasyGate(Transform root, Material structure, Material cyan, Material magenta, Material yellow)
        {
            GameObject gate = new GameObject("Crystal Victory Gate");
            gate.transform.SetParent(root, false);

            CreateBlock("Left Gate Pillar", gate.transform, new Vector3(-5.2f, 5.6f, 22.2f), new Vector3(0.8f, 10f, 0.9f), cyan);
            CreateBlock("Right Gate Pillar", gate.transform, new Vector3(5.2f, 5.6f, 22.2f), new Vector3(0.8f, 10f, 0.9f), magenta);
            CreateBlock("Gate Crown", gate.transform, new Vector3(0f, 10.1f, 22.2f), new Vector3(11.2f, 0.75f, 0.9f), structure);
            CreateBlock("Left Crown Slash", gate.transform, new Vector3(-3.2f, 11.1f, 22.05f), new Vector3(0.65f, 3.2f, 0.7f), cyan, new Vector3(0f, 0f, -42f));
            CreateBlock("Right Crown Slash", gate.transform, new Vector3(3.2f, 11.1f, 22.05f), new Vector3(0.65f, 3.2f, 0.7f), magenta, new Vector3(0f, 0f, 42f));
            CreateBlock("Gate Core", gate.transform, new Vector3(0f, 11.55f, 22f), new Vector3(1.4f, 1.4f, 0.8f), yellow, new Vector3(0f, 0f, 45f));
        }

        private static void CreateLightTowers(Transform root, Material structure, Material cyan, Material magenta, Material yellow)
        {
            GameObject towers = new GameObject("Festival Light Towers");
            towers.transform.SetParent(root, false);
            Vector3[] positions =
            {
                new Vector3(-11f, 0f, -8f), new Vector3(11f, 0f, -8f),
                new Vector3(-11f, 0f, 14f), new Vector3(11f, 0f, 14f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 origin = positions[i];
                Transform tower = new GameObject("Light Tower " + (i + 1)).transform;
                tower.SetParent(towers.transform, false);
                CreateCylinder("Mast", tower, origin + Vector3.up * 5.2f, new Vector3(0.24f, 5.2f, 0.24f), structure);
                CreateBlock("Light Bar", tower, origin + Vector3.up * 10.1f, new Vector3(3.2f, 0.32f, 0.48f), structure);
                CreateBlock("Cyan Lamp", tower, origin + new Vector3(-0.95f, 9.9f, -0.12f), new Vector3(0.68f, 0.68f, 0.44f), cyan);
                CreateBlock("Magenta Lamp", tower, origin + new Vector3(0f, 9.9f, -0.12f), new Vector3(0.68f, 0.68f, 0.44f), magenta);
                CreateBlock("Yellow Lamp", tower, origin + new Vector3(0.95f, 9.9f, -0.12f), new Vector3(0.68f, 0.68f, 0.44f), yellow);
            }
        }

        private static void CreateSpeakerPods(Transform root, Material structure, Material magenta, Material violet)
        {
            GameObject pods = new GameObject("Floating Speaker Pods");
            pods.transform.SetParent(root, false);
            Vector3[] positions =
            {
                new Vector3(-8.6f, 9.4f, 17.8f), new Vector3(8.6f, 9.4f, 17.8f),
                new Vector3(-13f, 7.4f, 3f), new Vector3(13f, 7.4f, 3f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pod = CreatePrimitive("Speaker Pod " + (i + 1), PrimitiveType.Sphere, pods.transform,
                    positions[i], new Vector3(1.7f, 1.7f, 1.1f), structure, Vector3.zero);
                CreateCylinder("Speaker Glow " + (i + 1), pod.transform, Vector3.back * 0.55f,
                    new Vector3(0.52f, 0.12f, 0.52f), i % 2 == 0 ? magenta : violet, new Vector3(90f, 0f, 0f));
            }
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, Vector3 rotation = default)
        {
            return CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material, rotation);
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, Vector3 rotation = default)
        {
            return CreatePrimitive(name, PrimitiveType.Cylinder, parent, position, scale, material, rotation);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material, Vector3 rotation)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localEulerAngles = rotation;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return gameObject;
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, Texture texture = null, Color? emission = null)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.mainTexture = texture;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", texture == null ? 0.38f : 0.12f);
            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void DestroyOwnedObject(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            }
        }
    }
}
