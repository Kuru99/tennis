using System;
using System.Collections.Generic;
using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.Presentation
{
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PrideCourtAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        public static PrideCourtAudio Instance { get; private set; }

        private AudioSource generalSource;
        private AudioSource hitSource;
        private AudioSource bounceSource;
        [Header("Ball Effect Audio")]
        [SerializeField] private AudioClip hitSoft;
        [SerializeField] private AudioClip hitStrong;
        [SerializeField] private AudioClip bounceSoft;
        [SerializeField] private AudioClip bounceHard;

        private AudioClip net;
        private AudioClip cardBuff;
        private AudioClip cardInstant;
        private AudioClip cardNextShot;
        private AudioClip cardCourt;
        private AudioClip cardCharacter;
        private AudioClip point;
        private AudioClip special;
        private AudioClip bounceSpecial;
        private readonly List<AudioClip> generatedClips = new List<AudioClip>();
        private bool initialized;

        public int HitPlayCount { get; private set; }
        public int BouncePlayCount { get; private set; }
        public bool HasPlayableBallEffects =>
            hitSoft != null && hitStrong != null &&
            bounceSoft != null && bounceHard != null && bounceSpecial != null;
        public bool IsHitEffectPlaying => hitSource != null && hitSource.isPlaying;
        public bool IsBounceEffectPlaying => bounceSource != null && bounceSource.isPlaying;

        private void Awake()
        {
            Instance = this;
            EnsureInitialized();
        }

        public static PrideCourtAudio EnsureAvailable()
        {
            if (Instance != null)
            {
                Instance.EnsureInitialized();
                return Instance;
            }

            PrideCourtAudio existing = UnityEngine.Object.FindAnyObjectByType<PrideCourtAudio>();
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureInitialized();
                return existing;
            }

            GameObject root = new GameObject("Pride Court Audio");
            PrideCourtAudio created = root.AddComponent<PrideCourtAudio>();
            created.EnsureInitialized();
            return created;
        }

        public static void PlayHitEffect(bool strong)
        {
            EnsureAvailable().PlayHit(strong);
        }

        public static void PlayBounceEffect(float impactSpeed, bool specialBounce, float bounceMultiplier, bool insideCourt)
        {
            EnsureAvailable().PlayBounce(impactSpeed, specialBounce, bounceMultiplier, insideCourt);
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            generalSource = GetComponent<AudioSource>();
            if (generalSource == null)
            {
                generalSource = gameObject.AddComponent<AudioSource>();
            }
            ConfigureSource(generalSource);
            hitSource = CreateEffectSource();
            bounceSource = CreateEffectSource();

            hitSoft = ResolveBallClip(hitSoft, "Audio/Ball/RacketHit_Soft", () => CreateRacketHit("Racket Hit Soft", false));
            hitStrong = ResolveBallClip(hitStrong, "Audio/Ball/RacketHit_Strong", () => CreateRacketHit("Racket Hit Strong", true));
            bounceSoft = ResolveBallClip(bounceSoft, "Audio/Ball/TennisBounce_Soft", () => CreateCourtBounce("Court Bounce Soft", false));
            bounceHard = ResolveBallClip(bounceHard, "Audio/Ball/TennisBounce_Hard", () => CreateCourtBounce("Court Bounce Hard", true));
            net = CreateGenerated(() => CreateTone("Net Cord", 145f, 0.12f, 12f, true));
            cardBuff = CreateGenerated(() => CreateSweep("Card Buff", 340f, 720f, 0.24f, 6f, 0.15f));
            cardInstant = CreateGenerated(() => CreateSweep("Card Instant", 760f, 1080f, 0.2f, 8f, 0.05f));
            cardNextShot = CreateGenerated(() => CreateSweep("Card Next Shot", 620f, 280f, 0.3f, 4.5f, 0.12f));
            cardCourt = CreateGenerated(() => CreateSweep("Card Court", 170f, 82f, 0.38f, 6.5f, 0.3f));
            cardCharacter = CreateGenerated(() => CreateSweep("Card Character", 410f, 940f, 0.34f, 4.8f, 0.08f));
            point = CreateGenerated(() => CreateTone("Point", 350f, 0.32f, 3f, false));
            special = CreateGenerated(() => CreateTone("Special", 105f, 0.55f, 2.5f, true));
            bounceSpecial = CreateGenerated(() => CreateSweep("Bounce Special", 510f, 245f, 0.22f, 5.5f, 0.08f));
            initialized = true;
        }

        private AudioClip ResolveBallClip(AudioClip assignedClip, string resourcePath, Func<AudioClip> fallbackFactory)
        {
            if (assignedClip != null)
            {
                return assignedClip;
            }

            AudioClip resourceClip = Resources.Load<AudioClip>(resourcePath);
            return resourceClip != null ? resourceClip : CreateGenerated(fallbackFactory);
        }

        private AudioClip CreateGenerated(Func<AudioClip> factory)
        {
            AudioClip clip = factory();
            if (clip != null)
            {
                generatedClips.Add(clip);
            }
            return clip;
        }

        private AudioSource CreateEffectSource()
        {
            AudioSource effectSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(effectSource);
            return effectSource;
        }

        private static void ConfigureSource(AudioSource configuredSource)
        {
            configuredSource.enabled = true;
            configuredSource.playOnAwake = false;
            configuredSource.loop = false;
            configuredSource.mute = false;
            configuredSource.volume = 1f;
            configuredSource.pitch = 1f;
            configuredSource.spatialBlend = 0f;
            configuredSource.dopplerLevel = 0f;
            configuredSource.priority = 96;
        }

        public void PlayHit(bool strong)
        {
            EnsureInitialized();
            AudioClip clip = strong ? hitStrong : hitSoft;
            float volume = strong ? 0.96f : 0.78f;
            if (Play(hitSource, clip, volume, strong ? 0.96f : 1.04f))
            {
                HitPlayCount++;
            }
        }

        public void PlayNet(bool tippedOver) => Play(generalSource, net, tippedOver ? 0.42f : 0.56f, tippedOver ? 1.2f : 0.82f);
        public void PlayCard() => Play(generalSource, cardBuff, 0.48f, 1f);

        public void PlayCard(CardCategory category, bool characterCard)
        {
            if (characterCard)
            {
                Play(generalSource, cardCharacter, 0.62f, 1f);
                return;
            }

            switch (category)
            {
                case CardCategory.Instant:
                    Play(generalSource, cardInstant, 0.56f, 1f);
                    break;
                case CardCategory.NextShot:
                    Play(generalSource, cardNextShot, 0.54f, 1f);
                    break;
                case CardCategory.Court:
                    Play(generalSource, cardCourt, 0.64f, 1f);
                    break;
                default:
                    Play(generalSource, cardBuff, 0.5f, 1f);
                    break;
            }
        }

        public void PlayPoint() => Play(generalSource, point, 0.55f, 1f);
        public void PlaySpecial() => Play(generalSource, special, 0.7f, 1f);

        public void PlayBounce(float impactSpeed, bool specialBounce, float bounceMultiplier, bool insideCourt)
        {
            EnsureInitialized();
            float strength = Mathf.InverseLerp(2f, 9f, impactSpeed);
            float pitch = Mathf.Lerp(0.9f, 1.12f, strength) * (insideCourt ? 1f : 0.82f);
            AudioClip clip;
            float volume;
            if (specialBounce)
            {
                clip = bounceSpecial;
                volume = Mathf.Lerp(0.72f, 0.9f, strength);
            }
            else if (impactSpeed >= 5.2f || bounceMultiplier >= 1.12f)
            {
                clip = bounceHard;
                volume = Mathf.Lerp(0.64f, 0.84f, strength);
            }
            else
            {
                clip = bounceSoft;
                volume = Mathf.Lerp(0.52f, 0.7f, strength);
            }

            if (Play(bounceSource, clip, volume, pitch))
            {
                BouncePlayCount++;
            }
        }

        private static bool Play(AudioSource targetSource, AudioClip clip, float volume, float pitch)
        {
            if (targetSource == null || clip == null)
            {
                return false;
            }

            targetSource.enabled = true;
            targetSource.mute = false;
            targetSource.Stop();
            targetSource.clip = clip;
            targetSource.volume = Mathf.Clamp01(volume);
            targetSource.pitch = pitch;
            targetSource.Play();
            return true;
        }

        private static AudioClip CreateRacketHit(string clipName, bool strong)
        {
            float duration = strong ? 0.17f : 0.13f;
            int length = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[length];
            uint state = strong ? 0xD1B54A35u : 0x94D049BBu;
            float baseFrequency = strong ? 132f : 158f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Clamp01(t * 900f);
                float bodyEnvelope = Mathf.Exp(-(strong ? 22f : 29f) * t) * attack;
                float stringEnvelope = Mathf.Exp(-38f * t) * attack;
                float snapEnvelope = Mathf.Exp(-105f * t) * attack;
                state = state * 1664525u + 1013904223u;
                float noise = ((state >> 8) & 0xFFFF) / 32767.5f - 1f;
                float body = Mathf.Sin(2f * Mathf.PI * baseFrequency * t) * 0.44f;
                float strings =
                    Mathf.Sin(2f * Mathf.PI * baseFrequency * 3.25f * t) * 0.3f +
                    Mathf.Sin(2f * Mathf.PI * baseFrequency * 5.8f * t) * 0.16f;
                float sample = body * bodyEnvelope + strings * stringEnvelope + noise * snapEnvelope * (strong ? 0.62f : 0.48f);
                samples[i] = Mathf.Clamp(sample, -0.96f, 0.96f);
            }
            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateCourtBounce(string clipName, bool hard)
        {
            float duration = hard ? 0.16f : 0.12f;
            int length = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[length];
            uint state = hard ? 0xA24BAED5u : 0x9FB21C65u;
            float thumpFrequency = hard ? 88f : 116f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Clamp01(t * 1100f);
                float thumpEnvelope = Mathf.Exp(-(hard ? 27f : 36f) * t) * attack;
                float tickEnvelope = Mathf.Exp(-145f * t) * attack;
                state = state * 1664525u + 1013904223u;
                float noise = ((state >> 8) & 0xFFFF) / 32767.5f - 1f;
                float pitchDrop = thumpFrequency * Mathf.Lerp(1.35f, 0.82f, Mathf.Clamp01(t / duration));
                float thump = Mathf.Sin(2f * Mathf.PI * pitchDrop * t) * (hard ? 0.68f : 0.54f);
                float surface = Mathf.Sin(2f * Mathf.PI * (hard ? 690f : 820f) * t) * 0.16f;
                float sample = thump * thumpEnvelope + (surface + noise * 0.5f) * tickEnvelope;
                samples[i] = Mathf.Clamp(sample, -0.94f, 0.94f);
            }
            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration, float decay, bool addNoise)
        {
            int length = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[length];
            uint state = 0x1234ABCDu;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-decay * t) * Mathf.Clamp01(t * 120f);
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.32f * Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * t);
                if (addNoise)
                {
                    state = state * 1664525u + 1013904223u;
                    wave += (((state >> 8) & 0xFFFF) / 32767.5f - 1f) * 0.28f;
                }
                samples[i] = wave * envelope * 0.48f;
            }
            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateSweep(
            string clipName,
            float startFrequency,
            float endFrequency,
            float duration,
            float decay,
            float noiseAmount)
        {
            int length = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[length];
            float phase = 0f;
            uint state = 0x9E3779B9u;
            for (int i = 0; i < length; i++)
            {
                float normalized = i / (float)Mathf.Max(1, length - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized * normalized);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float envelope = Mathf.Exp(-decay * normalized) * Mathf.Clamp01(normalized * 55f);
                float wave = Mathf.Sin(phase) + 0.38f * Mathf.Sin(phase * 2.03f) + 0.16f * Mathf.Sin(phase * 3.97f);
                state = state * 1664525u + 1013904223u;
                float noise = (((state >> 8) & 0xFFFF) / 32767.5f - 1f) * noiseAmount;
                samples[i] = (wave + noise) * envelope * 0.42f;
            }
            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateClip(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            for (int i = 0; i < generatedClips.Count; i++)
            {
                if (generatedClips[i] != null)
                {
                    Destroy(generatedClips[i]);
                }
            }
            generatedClips.Clear();
        }
    }
}
