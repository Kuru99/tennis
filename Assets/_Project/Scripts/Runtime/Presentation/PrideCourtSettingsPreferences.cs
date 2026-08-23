using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    public enum PrideCourtQualityPreset
    {
        Performance,
        Balanced,
        Quality
    }

    public static class PrideCourtSettingsPreferences
    {
        private const string VolumeKey = "PrideCourt.Settings.MasterVolume";
        private const string QualityKey = "PrideCourt.Settings.Quality";

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.85f));
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volume);
                PlayerPrefs.Save();
                AudioListener.volume = volume;
            }
        }

        public static PrideCourtQualityPreset QualityPreset
        {
            get => (PrideCourtQualityPreset)Mathf.Clamp(
                PlayerPrefs.GetInt(QualityKey, (int)PrideCourtQualityPreset.Balanced),
                (int)PrideCourtQualityPreset.Performance,
                (int)PrideCourtQualityPreset.Quality);
            set
            {
                PrideCourtQualityPreset preset = (PrideCourtQualityPreset)Mathf.Clamp(
                    (int)value,
                    (int)PrideCourtQualityPreset.Performance,
                    (int)PrideCourtQualityPreset.Quality);
                PlayerPrefs.SetInt(QualityKey, (int)preset);
                PlayerPrefs.Save();
                ApplyQuality(preset);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStoredSettings()
        {
            AudioListener.volume = MasterVolume;
            ApplyQuality(QualityPreset);
        }

        private static void ApplyQuality(PrideCourtQualityPreset preset)
        {
            int qualityCount = QualitySettings.names.Length;
            if (qualityCount <= 0) return;
            int index = preset switch
            {
                PrideCourtQualityPreset.Performance => 0,
                PrideCourtQualityPreset.Quality => qualityCount - 1,
                _ => qualityCount / 2
            };
            QualitySettings.SetQualityLevel(Mathf.Clamp(index, 0, qualityCount - 1), true);
        }
    }

    public static class SettingsAvailabilityPolicy
    {
        public static bool CanOpen(bool multiplayer, bool hasStarted, MatchPhase phase, bool serveRestrictionsActive)
        {
            if (!hasStarted || !multiplayer) return true;
            return phase == MatchPhase.CardSelection ||
                   (phase == MatchPhase.Serving && serveRestrictionsActive) ||
                   phase == MatchPhase.MatchOver;
        }
    }
}
