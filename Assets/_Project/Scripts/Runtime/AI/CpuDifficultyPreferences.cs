using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.AI
{
    public static class CpuDifficultyPreferences
    {
        private const string Key = "PrideCourt.Cpu.Difficulty";

        public static CpuDifficulty Current
        {
            get => (CpuDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)CpuDifficulty.Normal), 0, 2);
            set
            {
                PlayerPrefs.SetInt(Key, Mathf.Clamp((int)value, 0, 2));
                PlayerPrefs.Save();
            }
        }

        public static void Cycle()
        {
            Current = (CpuDifficulty)(((int)Current + 1) % 3);
        }
    }
}
