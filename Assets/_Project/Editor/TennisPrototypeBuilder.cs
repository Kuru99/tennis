using System;
using System.IO;
using PrideCourt.AI;
using PrideCourt.Cards;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Input;
using PrideCourt.Networking;
using PrideCourt.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrideCourt.Editor
{
    public static class TennisPrototypeBuilder
    {
        private const string GeneratedFolder = "Assets/_Project/Generated";
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/MVP_Prototype.unity";

        [MenuItem("Pride Court/Build MVP Prototype")]
        public static void BuildPrototype()
        {
            EnsureFolder("Assets/_Project", "Generated");
            EnsureFolder("Assets/_Project", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material courtMaterial = CreateOrLoadMaterial("Court_Navy", new Color(0.025f, 0.08f, 0.16f));
            Material lineMaterial = CreateOrLoadMaterial("Court_Lines", new Color(0.75f, 1f, 1f));
            Material nearMaterial = CreateOrLoadMaterial("Lux_Cyan", new Color(0.08f, 0.85f, 0.95f));
            Material farMaterial = CreateOrLoadMaterial("Bastion_Yellow", new Color(1f, 0.72f, 0.08f));
            Material ballMaterial = CreateOrLoadMaterial("Ball_Magenta", new Color(1f, 0.15f, 0.48f));

            CreateCourt(courtMaterial, lineMaterial);

            GameObject matchObject = new GameObject("Match Controller");
            TennisMatchController match = matchObject.AddComponent<TennisMatchController>();

            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Ball";
            ballObject.transform.localScale = Vector3.one * 0.32f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            Rigidbody ballBody = ballObject.AddComponent<Rigidbody>();
            ballBody.mass = 0.057f;
            ballBody.linearDamping = 0.04f;
            ballBody.angularDamping = 0.05f;
            TennisBallController ball = ballObject.AddComponent<TennisBallController>();

            TennisAthleteController near = CreateAthlete("Lux - Player", CourtSide.Near, AthleteIdentity.Lux, new Vector3(-TennisCourtGeometry.StartingLateralOffset, 1f, -TennisCourtGeometry.ServingBaseline), nearMaterial, true);
            TennisAthleteController far = CreateAthlete("Bastion - CPU", CourtSide.Far, AthleteIdentity.Bastion, new Vector3(TennisCourtGeometry.StartingLateralOffset, 1f, TennisCourtGeometry.ServingBaseline), farMaterial, false);

            KeyboardMouseCommandSource playerInput = near.GetComponent<KeyboardMouseCommandSource>();
            CpuCommandSource cpuInput = far.GetComponent<CpuCommandSource>();

            match.Configure(near, far, ball);
            ball.Configure(match);
            near.Configure(CourtSide.Near, AthleteIdentity.Lux, playerInput, match, ball);
            far.Configure(CourtSide.Far, AthleteIdentity.Bastion, cpuInput, match, ball);
            near.GetComponent<CardLoadoutController>().Configure(near, match, false);
            far.GetComponent<CardLoadoutController>().Configure(far, match, true);
            cpuInput.Configure(far, near, ball, match);
            matchObject.AddComponent<HudSettingsController>();
            AudioSource audioSource = matchObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            audioSource.mute = false;
            matchObject.AddComponent<PrideCourtAudio>();
            LanBattleSessionController lanSession = matchObject.AddComponent<LanBattleSessionController>();
            lanSession.Configure(match, near, far, ball);
            MvpFrontEndController frontEnd = matchObject.AddComponent<MvpFrontEndController>();
            frontEnd.Configure(match, near, far);

            CreateCamera(ball.transform);
            CreateLighting();
            TennisStadiumEnvironmentBuilder.BuildOrReplace();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = matchObject;
            Debug.Log("PRIDE_COURT_MVP_SCENE_BUILT: " + ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildPrototype();
            TennisPrototypeValidation.ValidateAll();
        }

        private static TennisAthleteController CreateAthlete(
            string name,
            CourtSide side,
            AthleteIdentity identity,
            Vector3 position,
            Material material,
            bool localPlayer)
        {
            GameObject athleteObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            athleteObject.name = name;
            athleteObject.transform.position = position;
            athleteObject.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            athleteObject.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(athleteObject.GetComponent<CapsuleCollider>());

            CharacterController characterController = athleteObject.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.48f;
            characterController.center = Vector3.zero;

            MonoBehaviour source = localPlayer
                ? athleteObject.AddComponent<KeyboardMouseCommandSource>()
                : athleteObject.AddComponent<CpuCommandSource>();
            TennisAthleteController athlete = athleteObject.AddComponent<TennisAthleteController>();
            athleteObject.AddComponent<CardLoadoutController>();
            athlete.Configure(side, identity, source, null, null);

            TennisGeneratedModelIntegrator.AttachGeneratedModels(athleteObject, identity);

            return athlete;
        }

        private static void CreateCourt(Material courtMaterial, Material lineMaterial)
        {
            GameObject court = GameObject.CreatePrimitive(PrimitiveType.Cube);
            court.name = "Official Neutral Court";
            court.transform.position = new Vector3(0f, -0.26f, 0f);
            court.transform.localScale = new Vector3(TennisCourtGeometry.VisualWidth, 0.5f, TennisCourtGeometry.VisualLength);
            court.GetComponent<Renderer>().sharedMaterial = courtMaterial;
            court.AddComponent<TennisCourtSurface>();

            CreateLine("Left Sideline", new Vector3(-TennisCourtGeometry.HalfWidth, 0.015f, 0f), new Vector3(0.07f, 0.025f, TennisCourtGeometry.VisualLength), lineMaterial);
            CreateLine("Right Sideline", new Vector3(TennisCourtGeometry.HalfWidth, 0.015f, 0f), new Vector3(0.07f, 0.025f, TennisCourtGeometry.VisualLength), lineMaterial);
            CreateLine("Near Baseline", new Vector3(0f, 0.015f, -11.85f * TennisCourtGeometry.Scale), new Vector3(11.05f * TennisCourtGeometry.Scale, 0.025f, 0.07f), lineMaterial);
            CreateLine("Far Baseline", new Vector3(0f, 0.015f, 11.85f * TennisCourtGeometry.Scale), new Vector3(11.05f * TennisCourtGeometry.Scale, 0.025f, 0.07f), lineMaterial);
            CreateLine("Near Service", new Vector3(0f, 0.015f, -TennisCourtGeometry.ServiceLineDepth), new Vector3(8.25f * TennisCourtGeometry.Scale, 0.025f, 0.06f), lineMaterial);
            CreateLine("Far Service", new Vector3(0f, 0.015f, TennisCourtGeometry.ServiceLineDepth), new Vector3(8.25f * TennisCourtGeometry.Scale, 0.025f, 0.06f), lineMaterial);
            CreateLine("Service Center", new Vector3(0f, 0.015f, 0f), new Vector3(0.06f, 0.025f, 12.8f * TennisCourtGeometry.Scale), lineMaterial);

            GameObject net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "Net";
            net.transform.position = new Vector3(0f, 0.48f, 0f);
            net.transform.localScale = new Vector3(11.35f * TennisCourtGeometry.Scale, 0.92f, 0.09f);
            net.GetComponent<Renderer>().sharedMaterial = lineMaterial;
            net.AddComponent<TennisNetSurface>();
            net.AddComponent<TennisNetVisual>().Configure(lineMaterial);
        }

        private static void CreateLine(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.position = position;
            line.transform.localScale = scale;
            line.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(line.GetComponent<BoxCollider>());
        }

        private static void CreateCamera(Transform ball)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraObject.AddComponent<AudioListener>();
            TennisCameraController cameraController = cameraObject.AddComponent<TennisCameraController>();
            cameraController.Configure(ball);
            cameraObject.transform.position = TennisCameraController.DefaultBasePosition;
            cameraObject.transform.rotation = Quaternion.LookRotation(
                TennisCameraController.DefaultLookTarget - cameraObject.transform.position,
                Vector3.up);
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Arena Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.9f, 0.95f, 1f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.17f, 0.2f, 0.3f);
        }

        private static Material CreateOrLoadMaterial(string name, Color color)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.42f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
