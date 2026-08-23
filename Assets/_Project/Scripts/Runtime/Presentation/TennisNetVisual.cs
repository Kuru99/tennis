using UnityEngine;

namespace PrideCourt.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer), typeof(BoxCollider))]
    public sealed class TennisNetVisual : MonoBehaviour
    {
        private const string VisualRootName = "Net Grid Visual";

        [SerializeField] private Material netMaterial;
        [SerializeField, Min(3)] private int verticalCordCount = 27;
        [SerializeField, Min(2)] private int horizontalCordCount = 5;
        [SerializeField] private Color cordColor = new Color(0.18f, 0.72f, 0.82f, 1f);
        [SerializeField] private Color tapeColor = new Color(0.78f, 1f, 1f, 1f);

        public void Configure(Material configuredMaterial)
        {
            netMaterial = configuredMaterial;
        }

        private void Awake()
        {
            BuildGridVisual();
        }

        private void BuildGridVisual()
        {
            Renderer solidRenderer = GetComponent<Renderer>();
            netMaterial ??= solidRenderer.sharedMaterial;
            solidRenderer.enabled = false;

            if (transform.Find(VisualRootName) != null)
            {
                return;
            }

            float worldWidth = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            float worldHeight = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
            float worldDepth = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.z));

            Transform visualRoot = new GameObject(VisualRootName).transform;
            visualRoot.SetParent(transform, false);

            float cordWidth = 0.026f / worldWidth;
            float cordHeight = 0.026f / worldHeight;
            float cordDepth = 0.04f / worldDepth;

            int verticalCount = Mathf.Max(3, verticalCordCount);
            for (int i = 0; i < verticalCount; i++)
            {
                float x = Mathf.Lerp(-0.47f, 0.47f, i / (float)(verticalCount - 1));
                CreateBar(
                    visualRoot,
                    $"Vertical Cord {i + 1:00}",
                    new Vector3(x, -0.02f, 0f),
                    new Vector3(cordWidth, 0.88f, cordDepth),
                    cordColor);
            }

            int horizontalCount = Mathf.Max(2, horizontalCordCount);
            for (int i = 0; i < horizontalCount; i++)
            {
                float y = Mathf.Lerp(-0.38f, 0.34f, i / (float)(horizontalCount - 1));
                CreateBar(
                    visualRoot,
                    $"Horizontal Cord {i + 1:00}",
                    new Vector3(0f, y, 0f),
                    new Vector3(0.94f, cordHeight, cordDepth),
                    cordColor);
            }

            CreateBar(
                visualRoot,
                "Top Tape",
                new Vector3(0f, 0.46f, 0f),
                new Vector3(1.02f, 0.075f / worldHeight, 0.13f / worldDepth),
                tapeColor);
            CreateBar(
                visualRoot,
                "Center Strap",
                new Vector3(0f, -0.01f, 0f),
                new Vector3(0.045f / worldWidth, 0.94f, 0.075f / worldDepth),
                tapeColor);
            CreateBar(
                visualRoot,
                "Left Post",
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.12f / worldWidth, 1.12f, 0.16f / worldDepth),
                tapeColor);
            CreateBar(
                visualRoot,
                "Right Post",
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.12f / worldWidth, 1.12f, 0.16f / worldDepth),
                tapeColor);
        }

        private void CreateBar(
            Transform parent,
            string barName,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = barName;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;

            Renderer barRenderer = bar.GetComponent<Renderer>();
            barRenderer.sharedMaterial = netMaterial;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            barRenderer.SetPropertyBlock(properties);

            Destroy(bar.GetComponent<Collider>());
        }
    }
}
