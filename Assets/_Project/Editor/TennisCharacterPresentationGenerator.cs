using System;
using PrideCourt.Domain;
using UnityEditor;
using UnityEngine;

namespace PrideCourt.Editor
{
    public static class TennisCharacterPresentationGenerator
    {
        private const string PortraitResourceFolder = "CharacterPortraits/";
        private const string CutInResourceFolder = "SpecialCutIns/";
        private static readonly AthleteIdentity[] PresentationIdentities =
        {
            AthleteIdentity.Lux,
            AthleteIdentity.Bastion,
            AthleteIdentity.Lucia,
            AthleteIdentity.Charlotte,
            AthleteIdentity.Zephyr,
            AthleteIdentity.Poko
        };

        [MenuItem("Pride Court/Validate 2D Character Presentation")]
        public static void ValidateStaticPresentation()
        {
            foreach (AthleteIdentity identity in PresentationIdentities)
            {
                ValidateTexture(PortraitResourceFolder + identity, identity, "portrait", 2f / 3f);
                ValidateTexture(CutInResourceFolder + identity, identity, "cut-in", 16f / 9f);
            }

            ValidateTexture("CharacterCardAtlas", null, "opening atlas", 2f);
            ValidateTexture("SpecialCutIn", null, "fallback cut-in", 16f / 9f);
            Debug.Log("PRIDE_COURT_2D_CHARACTER_PRESENTATION_VALIDATED");
        }

        public static void GenerateFromCommandLine()
        {
            ValidateStaticPresentation();
        }

        private static void ValidateTexture(string resourcePath, AthleteIdentity? identity, string role,
            float expectedAspect)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            string subject = identity.HasValue ? identity.Value.ToString() : resourcePath;
            if (texture == null)
            {
                throw new InvalidOperationException(
                    $"PRIDE_COURT_2D_PRESENTATION_FAILED: Missing {role} for {subject}: Resources/{resourcePath}");
            }

            if (texture.width < 512 || texture.height < 512)
            {
                throw new InvalidOperationException(
                    $"PRIDE_COURT_2D_PRESENTATION_FAILED: {role} for {subject} is too small: " +
                    $"{texture.width}x{texture.height}");
            }

            float actualAspect = texture.width / (float)texture.height;
            if (Mathf.Abs(actualAspect - expectedAspect) > 0.15f)
            {
                throw new InvalidOperationException(
                    $"PRIDE_COURT_2D_PRESENTATION_FAILED: Unexpected aspect for {role} of {subject}: " +
                    $"{texture.width}x{texture.height}");
            }
        }
    }
}
