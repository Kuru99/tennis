using UnityEngine;

namespace PrideCourt.Presentation
{
    public enum BounceEffectStrength
    {
        Normal,
        Hard,
        Special
    }

    public readonly struct BounceEffectProfile
    {
        public BounceEffectProfile(BounceEffectStrength strength, Color accent, float radius, int particleCount, float duration)
        {
            Strength = strength;
            Accent = accent;
            Radius = radius;
            ParticleCount = particleCount;
            Duration = duration;
        }

        public BounceEffectStrength Strength { get; }
        public Color Accent { get; }
        public float Radius { get; }
        public int ParticleCount { get; }
        public float Duration { get; }
    }

    public sealed class BallBounceEffect : MonoBehaviour
    {
        private const int RingSegments = 44;

        private BounceEffectProfile profile;
        private float elapsed;
        private Material effectMaterial;
        private LineRenderer[] rings;

        public static int PlayCount { get; private set; }

        public static void Play(Vector3 position, float impactSpeed, bool special, float bounceMultiplier, bool insideCourt)
        {
            BounceEffectProfile resolved = GetProfile(impactSpeed, special, bounceMultiplier, insideCourt);
            GameObject root = new GameObject("ボールバウンドエフェクト")
            {
                hideFlags = HideFlags.DontSave
            };
            root.transform.position = position + Vector3.up * 0.035f;
            BallBounceEffect effect = root.AddComponent<BallBounceEffect>();
            effect.Configure(resolved);
            PlayCount++;
        }

        public static BounceEffectProfile GetProfile(
            float impactSpeed,
            bool special,
            float bounceMultiplier,
            bool insideCourt)
        {
            if (special)
            {
                return new BounceEffectProfile(
                    BounceEffectStrength.Special,
                    new Color(0.9f, 0.28f, 1f, 1f),
                    1.2f,
                    22,
                    0.58f);
            }

            bool hard = impactSpeed >= 5.2f || bounceMultiplier >= 1.12f;
            Color accent = !insideCourt
                ? PrideCourtUiTheme.Magenta
                : hard ? PrideCourtUiTheme.Yellow : PrideCourtUiTheme.Cyan;
            return hard
                ? new BounceEffectProfile(BounceEffectStrength.Hard, accent, 0.95f, 16, 0.5f)
                : new BounceEffectProfile(BounceEffectStrength.Normal, accent, 0.68f, 11, 0.42f);
        }

        private void Configure(BounceEffectProfile configuredProfile)
        {
            profile = configuredProfile;
            effectMaterial = PrideCourtEffectMaterialFactory.Create("バウンドエフェクトマテリアル");
            CreateParticles();
            CreateRings();
        }

        private void CreateParticles()
        {
            GameObject particleObject = new GameObject("バウンド粒子");
            particleObject.transform.SetParent(transform, false);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = profile.Duration;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(profile.Duration * 0.45f, profile.Duration * 0.9f);
            main.startSpeed = profile.Strength == BounceEffectStrength.Normal
                ? new ParticleSystem.MinMaxCurve(0.55f, 1.25f)
                : new ParticleSystem.MinMaxCurve(0.9f, 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(profile.Accent, Color.white);
            main.gravityModifier = 0.32f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)profile.ParticleCount) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = profile.Strength == BounceEffectStrength.Special ? 32f : 22f;
            shape.radius = profile.Radius * 0.16f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(profile.Accent, 0.24f),
                    new GradientColorKey(profile.Accent, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.72f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = effectMaterial;
            particles.Play(true);
        }

        private void CreateRings()
        {
            int ringCount = profile.Strength == BounceEffectStrength.Special ? 2 : 1;
            rings = new LineRenderer[ringCount];
            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                GameObject ringObject = new GameObject("バウンドリング " + (ringIndex + 1));
                ringObject.transform.SetParent(transform, false);
                ringObject.transform.localPosition = Vector3.up * (ringIndex * 0.018f);
                LineRenderer line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = RingSegments;
                line.widthMultiplier = profile.Strength == BounceEffectStrength.Normal ? 0.032f : 0.052f;
                line.numCornerVertices = 2;
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
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, profile.Duration));
            for (int i = 0; i < rings.Length; i++)
            {
                float delay = i * 0.12f;
                float progress = Mathf.Clamp01((normalized - delay) / Mathf.Max(0.01f, 1f - delay));
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                rings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.12f, profile.Radius, eased);
                float alpha = Mathf.Pow(1f - progress, 1.5f);
                Color color = new Color(profile.Accent.r, profile.Accent.g, profile.Accent.b, alpha);
                rings[i].startColor = color;
                rings[i].endColor = new Color(1f, 1f, 1f, alpha * 0.55f);
            }

            if (elapsed >= profile.Duration + 0.08f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (effectMaterial != null) Destroy(effectMaterial);
        }
    }
}
