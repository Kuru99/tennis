using UnityEngine;

namespace PrideCourt.Presentation
{
    public static class PrideCourtEffectMaterialFactory
    {
        private static Texture2D glowTexture;

        public static Material Create(string materialName)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GlowTexture
            };
        }

        private static Texture2D GlowTexture
        {
            get
            {
                if (glowTexture != null) return glowTexture;
                const int size = 32;
                glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "プライドコート共通グロー",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                Color32[] pixels = new Color32[size * size];
                Vector2 center = Vector2.one * (size - 1) * 0.5f;
                float radius = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                glowTexture.SetPixels32(pixels);
                glowTexture.Apply(false, true);
                return glowTexture;
            }
        }
    }
}
