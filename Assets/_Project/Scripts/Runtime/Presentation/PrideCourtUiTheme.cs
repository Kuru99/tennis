// Hallmark · pre-emit critique: P5 H5 E4 S5 R4 V5
// Hallmark · playful · pop-punk Sport · cyan/magenta · Workbench · contrast/slop/tokens/responsive: pass
using System.Collections.Generic;
using UnityEngine;

namespace PrideCourt.Presentation
{
    // Hallmark · genre: playful · macrostructure: Workbench · theme: Sport · anchor: cyan/magenta
    public static class PrideCourtUiTheme
    {
        public enum Tone
        {
            Cyan,
            Magenta,
            Violet,
            Yellow,
            Neutral
        }

        public static readonly Color Ink = new Color(0.018f, 0.028f, 0.075f, 0.98f);
        public static readonly Color Surface = new Color(0.035f, 0.065f, 0.145f, 0.98f);
        public static readonly Color Raised = new Color(0.065f, 0.105f, 0.21f, 0.98f);
        public static readonly Color Paper = new Color(0.91f, 0.965f, 1f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.71f, 0.84f, 1f);
        public static readonly Color Cyan = new Color(0.08f, 0.88f, 0.98f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.16f, 0.48f, 1f);
        public static readonly Color Violet = new Color(0.61f, 0.29f, 0.98f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.75f, 0.08f, 1f);

        public static string FormatGameStars(int games, int gamesToWin)
        {
            int slotCount = Mathf.Max(1, gamesToWin);
            int filledCount = Mathf.Clamp(games, 0, slotCount);
            string stars = string.Empty;
            for (int i = 0; i < slotCount; i++)
            {
                if (i > 0) stars += " ";
                stars += i < filledCount ? "★" : "☆";
            }

            return stars;
        }

        private const int TextureSize = 64;
        private const int Cut = 10;
        private static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        private static Font displayFont;
        private static Font bodyFont;

        public static Font DisplayFont => displayFont != null
            ? displayFont
            : displayFont = Resources.Load<Font>("Fonts/DelaGothicOne-Regular");

        public static Font BodyFont => bodyFont != null
            ? bodyFont
            : bodyFont = Resources.Load<Font>("Fonts/NotoSansJP-VF");

        public static bool FontsAvailable => DisplayFont != null && BodyFont != null;

        public static Color ToneColor(Tone tone)
        {
            return tone switch
            {
                Tone.Magenta => Magenta,
                Tone.Violet => Violet,
                Tone.Yellow => Yellow,
                Tone.Neutral => Muted,
                _ => Cyan
            };
        }

        public static GUIStyle Heading(float scale, TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = DisplayFont,
                fontSize = Mathf.Max(18, Mathf.RoundToInt(34f * scale)),
                alignment = alignment,
                clipping = TextClipping.Clip,
                normal = { textColor = color ?? Paper }
            };
        }

        public static GUIStyle Label(float scale, int size = 15, TextAnchor alignment = TextAnchor.MiddleLeft,
            Color? color = null, bool wrap = false)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = BodyFont,
                fontSize = Mathf.Max(10, Mathf.RoundToInt(size * scale)),
                alignment = alignment,
                wordWrap = wrap,
                clipping = wrap ? TextClipping.Overflow : TextClipping.Clip,
                normal = { textColor = color ?? Paper }
            };
        }

        public static GUIStyle Panel(float scale = 1f)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                font = DisplayFont,
                fontSize = Mathf.Max(13, Mathf.RoundToInt(19f * scale)),
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(Mathf.RoundToInt(16f * scale), Mathf.RoundToInt(16f * scale),
                    Mathf.RoundToInt(9f * scale), Mathf.RoundToInt(12f * scale)),
                border = new RectOffset(14, 14, 14, 14),
                normal = { background = ShapeTexture(Surface, Cyan, 2), textColor = Cyan }
            };
            return style;
        }

        public static GUIStyle CardPanel(Color accent, float scale = 1f)
        {
            return new GUIStyle(Panel(scale))
            {
                normal = { background = ShapeTexture(Raised, accent, 2), textColor = accent }
            };
        }

        public static GUIStyle Button(Tone tone, float scale = 1f, int fontSize = 17)
        {
            Color accent = ToneColor(tone);
            Color hover = Color.Lerp(accent, Paper, 0.18f);
            Color pressed = Color.Lerp(accent, Ink, 0.28f);
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                font = DisplayFont,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * scale)),
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(14, 14, 14, 14),
                padding = new RectOffset(Mathf.RoundToInt(10f * scale), Mathf.RoundToInt(10f * scale), 4, 4)
            };
            style.normal.background = ShapeTexture(Raised, accent, 3);
            style.normal.textColor = Paper;
            style.hover.background = ShapeTexture(Color.Lerp(Raised, accent, 0.18f), hover, 3);
            style.hover.textColor = Color.white;
            style.active.background = ShapeTexture(pressed, Paper, 3);
            style.active.textColor = Ink;
            style.focused.background = style.hover.background;
            style.focused.textColor = style.hover.textColor;
            style.onNormal = style.normal;
            style.onHover = style.hover;
            style.onActive = style.active;
            style.onFocused = style.focused;
            return style;
        }

        public static GUIStyle Toggle(float scale)
        {
            GUIStyle style = new GUIStyle(GUI.skin.toggle)
            {
                font = BodyFont,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(15f * scale)),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(Mathf.RoundToInt(28f * scale), 4, 2, 2)
            };
            style.normal.textColor = Paper;
            style.hover.textColor = Cyan;
            style.onNormal.textColor = Magenta;
            style.onHover.textColor = Color.white;
            return style;
        }

        public static void DrawPanel(Rect rect, Tone tone = Tone.Cyan, float scale = 1f, string title = null)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.52f);
            GUI.Box(new Rect(rect.x + 7f * scale, rect.y + 7f * scale, rect.width, rect.height), GUIContent.none,
                new GUIStyle(Panel(scale)) { normal = { background = ShapeTexture(Color.black, Color.black, 0) } });
            GUI.color = previous;
            GUI.Box(rect, title ?? string.Empty, Panel(scale));

            Color accent = ToneColor(tone);
            DrawSolid(new Rect(rect.x + 18f * scale, rect.y - 2f * scale, Mathf.Min(rect.width * 0.28f, 170f * scale), 5f * scale), accent);
            DrawSolid(new Rect(rect.xMax - 46f * scale, rect.yMax - 5f * scale, 30f * scale, 5f * scale), accent);
            DrawSlash(new Rect(rect.xMax - 82f * scale, rect.y + 8f * scale, 9f * scale, 24f * scale), accent, -18f);
            DrawSlash(new Rect(rect.xMax - 64f * scale, rect.y + 8f * scale, 9f * scale, 24f * scale), Magenta, -18f);
        }

        public static void DrawTag(Rect rect, string text, Tone tone, float scale = 1f)
        {
            Color accent = ToneColor(tone);
            GUI.Box(rect, GUIContent.none, new GUIStyle(Panel(scale))
            {
                normal = { background = ShapeTexture(accent, accent, 0) }
            });
            GUI.Label(rect, text, Label(scale, 13, TextAnchor.MiddleCenter, Ink));
        }

        public static void DrawBar(Rect rect, float normalized, Color accent, string label, float scale)
        {
            normalized = Mathf.Clamp01(normalized);
            GUI.Box(rect, GUIContent.none, CardPanel(Muted, scale));
            Rect fill = new Rect(rect.x + 3f, rect.y + 3f, Mathf.Max(0f, (rect.width - 6f) * normalized), rect.height - 6f);
            DrawSolid(fill, accent);
            if (!string.IsNullOrEmpty(label))
                GUI.Label(new Rect(rect.x + 8f * scale, rect.y, rect.width - 16f * scale, rect.height), label,
                    Label(scale, 11, TextAnchor.MiddleLeft, Paper));
        }

        public static void DrawTouchButton(Rect rect, string key, string action, Tone tone, float scale)
        {
            GUI.Box(rect, GUIContent.none, Button(tone, scale));
            float keyHeight = Mathf.Min(rect.height * 0.54f, 46f * scale);
            GUI.Label(new Rect(rect.x, rect.y + 3f * scale, rect.width, keyHeight), key,
                Heading(scale * 0.92f, TextAnchor.MiddleCenter, ToneColor(tone)));
            GUI.Label(new Rect(rect.x + 3f, rect.y + keyHeight - 1f, rect.width - 6f, rect.height - keyHeight), action,
                Label(scale, 11, TextAnchor.MiddleCenter, Paper));
        }

        public static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawSlash(Rect rect, Color color, float degrees)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, rect.center);
            DrawSolid(rect, color);
            GUI.matrix = previous;
        }

        private static Texture2D ShapeTexture(Color fill, Color border, int borderWidth)
        {
            int key = ColorKey(fill, border, borderWidth);
            if (textures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Pride Court UI Shape",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            Color32 fill32 = fill;
            Color32 border32 = border;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    bool inside = InsideShape(x, y, 0, Cut);
                    bool interior = borderWidth <= 0 || InsideShape(x, y, borderWidth, Cut + borderWidth);
                    pixels[y * TextureSize + x] = !inside
                        ? new Color32(0, 0, 0, 0)
                        : interior ? fill32 : border32;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            textures[key] = texture;
            return texture;
        }

        private static bool InsideShape(int x, int y, int inset, int cut)
        {
            int max = TextureSize - 1 - inset;
            if (x < inset || y < inset || x > max || y > max) return false;
            return x + y >= cut && x + (TextureSize - 1 - y) >= cut &&
                   (TextureSize - 1 - x) + y >= cut &&
                   (TextureSize - 1 - x) + (TextureSize - 1 - y) >= cut;
        }

        private static int ColorKey(Color fill, Color border, int borderWidth)
        {
            Color32 f = fill;
            Color32 b = border;
            unchecked
            {
                int hash = f.r | f.g << 8 | f.b << 16 | f.a << 24;
                hash = hash * 397 ^ (b.r | b.g << 8 | b.b << 16 | b.a << 24);
                return hash * 397 ^ borderWidth;
            }
        }
    }
}
