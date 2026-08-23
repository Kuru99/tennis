using PrideCourt.Cards;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using UnityEngine;

namespace PrideCourt.Presentation
{
    public enum CardEffectStyle
    {
        Aura,
        Pulse,
        Spiral,
        CourtWave
    }

    public readonly struct CardEffectProfile
    {
        public CardEffectProfile(CardEffectStyle style, Color accent, float radius, int particleCount, float duration)
        {
            Style = style;
            Accent = accent;
            Radius = radius;
            ParticleCount = particleCount;
            Duration = duration;
        }

        public CardEffectStyle Style { get; }
        public Color Accent { get; }
        public float Radius { get; }
        public int ParticleCount { get; }
        public float Duration { get; }
    }

    public sealed class CardActivationEffect : MonoBehaviour
    {
        private const int RingSegments = 56;
        private CardEffectProfile profile;
        private Transform followTarget;
        private Vector3 worldAnchor;
        private string displayName;
        private float elapsed;
        private Material effectMaterial;
        private LineRenderer[] rings;
        private Transform particleTransform;

        public static int PlayCount { get; private set; }

        public static void Play(CardId card, TennisAthleteController athlete)
        {
            if (athlete == null) return;

            CardDefinition definition = CardCatalog.Get(card);
            CardEffectProfile resolved = GetProfile(card);
            GameObject root = new GameObject("カード発動エフェクト - " + definition.DisplayName)
            {
                hideFlags = HideFlags.DontSave
            };
            CardActivationEffect effect = root.AddComponent<CardActivationEffect>();
            effect.Configure(definition.DisplayName, resolved, athlete.transform);
            PlayCount++;
        }

        public static CardEffectProfile GetProfile(CardId card)
        {
            CardDefinition definition = CardCatalog.Get(card);
            Color accent = card switch
            {
                CardId.AccelStep => PrideCourtUiTheme.Cyan,
                CardId.EcoRun => new Color(0.18f, 1f, 0.58f, 1f),
                CardId.RecoveryPulse => new Color(1f, 0.32f, 0.62f, 1f),
                CardId.SpinBoost => PrideCourtUiTheme.Violet,
                CardId.GaugeCharge => PrideCourtUiTheme.Yellow,
                CardId.GripCourt => new Color(0.12f, 0.92f, 0.78f, 1f),
                CardId.SlipCourt => PrideCourtUiTheme.Magenta,
                CardId.HighBounce => new Color(1f, 0.82f, 0.12f, 1f),
                CardId.FlashStep => new Color(0.15f, 0.95f, 1f, 1f),
                CardId.TailFeint => new Color(0.92f, 0.24f, 1f, 1f),
                CardId.RailBoost => new Color(1f, 0.68f, 0.08f, 1f),
                CardId.AnchorCore => new Color(0.2f, 0.72f, 1f, 1f),
                _ => PrideCourtUiTheme.Cyan
            };

            return definition.Category switch
            {
                CardCategory.Instant => new CardEffectProfile(CardEffectStyle.Pulse, accent, 1.25f, 30, 1.05f),
                CardCategory.NextShot => new CardEffectProfile(CardEffectStyle.Spiral, accent, 1.1f, 34, 1.2f),
                CardCategory.Court => new CardEffectProfile(CardEffectStyle.CourtWave, accent, 11.2f, 48, 1.35f),
                _ => new CardEffectProfile(CardEffectStyle.Aura, accent, 1.05f, 28, 1.15f)
            };
        }

        private void Configure(string cardDisplayName, CardEffectProfile configuredProfile, Transform athlete)
        {
            displayName = cardDisplayName;
            profile = configuredProfile;
            followTarget = profile.Style == CardEffectStyle.CourtWave ? null : athlete;
            worldAnchor = profile.Style == CardEffectStyle.CourtWave
                ? new Vector3(0f, 0.08f, 0f)
                : athlete.position + Vector3.up * 0.12f;
            transform.position = worldAnchor;
            effectMaterial = PrideCourtEffectMaterialFactory.Create("カード発動エフェクトマテリアル");
            CreateParticles();
            CreateRings();
        }

        private void CreateParticles()
        {
            GameObject particleObject = new GameObject("発光パーティクル");
            particleObject.transform.SetParent(transform, false);
            particleTransform = particleObject.transform;
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = profile.Duration;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(profile.Duration * 0.38f, profile.Duration * 0.78f);
            main.startSpeed = profile.Style == CardEffectStyle.CourtWave
                ? new ParticleSystem.MinMaxCurve(2.8f, 5.2f)
                : new ParticleSystem.MinMaxCurve(0.9f, 2.4f);
            main.startSize = profile.Style == CardEffectStyle.CourtWave
                ? new ParticleSystem.MinMaxCurve(0.12f, 0.28f)
                : new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(profile.Accent, Color.white);
            main.gravityModifier = profile.Style == CardEffectStyle.CourtWave ? 0.08f : -0.14f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)profile.ParticleCount),
                new ParticleSystem.Burst(profile.Duration * 0.26f, (short)Mathf.Max(6, profile.ParticleCount / 3))
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            if (profile.Style == CardEffectStyle.CourtWave || profile.Style == CardEffectStyle.Spiral)
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = profile.Style == CardEffectStyle.CourtWave ? 1.1f : profile.Radius;
                particleObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = profile.Radius;
                shape.radiusThickness = 0.28f;
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(profile.Accent, 0.25f),
                    new GradientColorKey(profile.Accent * 0.55f, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(0.85f, 0.58f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.15f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = effectMaterial;
            particles.Play(true);
        }

        private void CreateRings()
        {
            int ringCount = profile.Style == CardEffectStyle.CourtWave ? 3 : 2;
            rings = new LineRenderer[ringCount];
            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                GameObject ringObject = new GameObject("衝撃リング " + (ringIndex + 1));
                ringObject.transform.SetParent(transform, false);
                ringObject.transform.localPosition = Vector3.up * (0.025f + ringIndex * 0.02f);
                LineRenderer line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = RingSegments;
                line.widthMultiplier = profile.Style == CardEffectStyle.CourtWave ? 0.075f : 0.05f;
                line.numCornerVertices = 3;
                line.numCapVertices = 3;
                line.sharedMaterial = effectMaterial;
                for (int i = 0; i < RingSegments; i++)
                {
                    float angle = i / (float)RingSegments * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                }
                rings[ringIndex] = line;
            }
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (followTarget != null)
            {
                worldAnchor = followTarget.position + Vector3.up * 0.12f;
                transform.position = worldAnchor;
            }

            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, profile.Duration));
            float endScale = profile.Style == CardEffectStyle.CourtWave ? profile.Radius : profile.Radius * 1.8f;
            for (int i = 0; i < rings.Length; i++)
            {
                float delay = i * 0.14f;
                float ringProgress = Mathf.Clamp01((normalized - delay) / Mathf.Max(0.01f, 1f - delay));
                float eased = 1f - Mathf.Pow(1f - ringProgress, 3f);
                rings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.18f, endScale, eased);
                float alpha = Mathf.Pow(1f - ringProgress, 1.6f);
                Color ringColor = new Color(profile.Accent.r, profile.Accent.g, profile.Accent.b, alpha);
                rings[i].startColor = ringColor;
                rings[i].endColor = new Color(1f, 1f, 1f, alpha * 0.7f);
            }

            if (profile.Style == CardEffectStyle.Spiral && particleTransform != null)
            {
                particleTransform.Rotate(Vector3.forward, 260f * Time.unscaledDeltaTime, Space.Self);
            }

            if (elapsed >= profile.Duration + 0.12f)
            {
                Destroy(gameObject);
            }
        }

        private void OnGUI()
        {
            if (HudSettingsController.IsAnyOpen) return;
            Camera camera = Camera.main;
            if (camera == null || elapsed > profile.Duration) return;
            Vector3 screenPoint = camera.WorldToScreenPoint(worldAnchor + Vector3.up *
                (profile.Style == CardEffectStyle.CourtWave ? 0.5f : 2.15f));
            if (screenPoint.z <= 0f) return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.25f);
            float alpha = Mathf.Clamp01(1f - elapsed / profile.Duration);
            float x = screenPoint.x - 150f * scale;
            float y = Screen.height - screenPoint.y - 38f * scale;
            GUI.Label(new Rect(x, y, 300f * scale, 24f * scale), "カード発動",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter,
                    new Color(profile.Accent.r, profile.Accent.g, profile.Accent.b, alpha)));
            GUI.Label(new Rect(x, y + 20f * scale, 300f * scale, 42f * scale), displayName,
                PrideCourtUiTheme.Heading(scale * 0.58f, TextAnchor.MiddleCenter,
                    new Color(PrideCourtUiTheme.Paper.r, PrideCourtUiTheme.Paper.g, PrideCourtUiTheme.Paper.b, alpha)));
        }

        private void OnDestroy()
        {
            if (effectMaterial != null) Destroy(effectMaterial);
        }
    }
}
