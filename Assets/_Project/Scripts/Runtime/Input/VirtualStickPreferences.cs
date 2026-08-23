using UnityEngine;

namespace PrideCourt.Input
{
    public enum VirtualStickSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    public static class VirtualStickPreferences
    {
        private const string PreferenceKey = "PrideCourt.VirtualStickSize";

        public static VirtualStickSize Current
        {
            get => (VirtualStickSize)Mathf.Clamp(
                PlayerPrefs.GetInt(PreferenceKey, (int)VirtualStickSize.Medium),
                (int)VirtualStickSize.Small,
                (int)VirtualStickSize.Large);
            set
            {
                VirtualStickSize clamped = (VirtualStickSize)Mathf.Clamp(
                    (int)value,
                    (int)VirtualStickSize.Small,
                    (int)VirtualStickSize.Large);
                PlayerPrefs.SetInt(PreferenceKey, (int)clamped);
                PlayerPrefs.Save();
            }
        }

        public static float Scale => ScaleFor(Current);

        public static float ScaleFor(VirtualStickSize size)
        {
            return size switch
            {
                VirtualStickSize.Small => 0.82f,
                VirtualStickSize.Large => 1.18f,
                _ => 1f
            };
        }
    }
}
