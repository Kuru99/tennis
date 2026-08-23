using UnityEngine;

namespace PrideCourt.Gameplay
{
    public static class HudPreferences
    {
        private const string VisibleKey = "PrideCourt.Hud.Visible";
        private const string CardsKey = "PrideCourt.Hud.Cards";
        private const string ResourcesKey = "PrideCourt.Hud.Resources";
        private const string ScaleKey = "PrideCourt.Hud.Scale";

        public static bool Visible
        {
            get => PlayerPrefs.GetInt(VisibleKey, 1) != 0;
            set { PlayerPrefs.SetInt(VisibleKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool ShowCards
        {
            get => Visible && CardsEnabled;
            set => CardsEnabled = value;
        }

        public static bool CardsEnabled
        {
            get => PlayerPrefs.GetInt(CardsKey, 1) != 0;
            set { PlayerPrefs.SetInt(CardsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool ShowResources
        {
            get => Visible && ResourcesEnabled;
            set => ResourcesEnabled = value;
        }

        public static bool ResourcesEnabled
        {
            get => PlayerPrefs.GetInt(ResourcesKey, 1) != 0;
            set { PlayerPrefs.SetInt(ResourcesKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Scale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), 0.7f, 1.3f);
            set { PlayerPrefs.SetFloat(ScaleKey, Mathf.Clamp(value, 0.7f, 1.3f)); PlayerPrefs.Save(); }
        }
    }
}
