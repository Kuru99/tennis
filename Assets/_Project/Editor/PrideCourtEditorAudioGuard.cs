using UnityEditor;
using UnityEngine;

namespace PrideCourt.Editor
{
    [InitializeOnLoad]
    internal static class PrideCourtEditorAudioGuard
    {
        static PrideCourtEditorAudioGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EnsureEditorAudioEnabled();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                EnsureEditorAudioEnabled();
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                AudioListener.pause = false;
                AudioListener.volume = 1f;
            }
        }

        private static void EnsureEditorAudioEnabled()
        {
            if (!EditorUtility.audioMasterMute)
            {
                return;
            }

            EditorUtility.audioMasterMute = false;
            Debug.Log("[Pride Court] Unity Editor Mute Audio was disabled for Play Mode audio validation.");
        }
    }
}
