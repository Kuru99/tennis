using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PrideCourt.Editor
{
    public static class TennisBuildPipeline
    {
        private const string PublicVersion = "0.1.0-alpha";

        public static void BuildWindowsFromCommandLine()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(root, "Builds", "Windows");
            Directory.CreateDirectory(outputDirectory);
            string executable = Path.Combine(outputDirectory, "PrideCourtMVP.exe");

            PlayerSettings.companyName = "Pride Court Studio";
            PlayerSettings.productName = "プライド・コート";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.pridecourt.mvp");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/MVP_Prototype.unity" },
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("PRIDE_COURT_WINDOWS_BUILD_FAILED: " + report.summary.result);
            }

            Debug.Log("PRIDE_COURT_WINDOWS_BUILD_PASSED: " + executable);
            EditorApplication.Exit(0);
        }

        public static void BuildAndroidFromCommandLine()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(root, "Builds", "Android");
            Directory.CreateDirectory(outputDirectory);
            string apk = Path.Combine(outputDirectory, "PrideCourtMVP.apk");

            PlayerSettings.companyName = "Pride Court Studio";
            PlayerSettings.productName = "プライド・コート";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pridecourt.mvp");
            PlayerSettings.Android.forceInternetPermission = true;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/MVP_Prototype.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("PRIDE_COURT_ANDROID_BUILD_FAILED: " + report.summary.result);
            }

            Debug.Log("PRIDE_COURT_ANDROID_BUILD_PASSED: " + apk);
            EditorApplication.Exit(0);
        }

        public static void BuildPublicWindowsFromCommandLine()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(root, "Builds", "Release", "Windows", "PrideCourt-Windows-x64");
            Directory.CreateDirectory(outputDirectory);

            ConfigurePublicPlayerSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.pridecourt.mvp");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/MVP_Prototype.unity" },
                locationPathName = Path.Combine(outputDirectory, "PrideCourt.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            RequireSuccessfulBuild(options, "PRIDE_COURT_PUBLIC_WINDOWS_BUILD_PASSED");
        }

        public static void BuildPublicAndroidFromCommandLine()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(root, "Builds", "Release", "Android");
            Directory.CreateDirectory(outputDirectory);

            ConfigurePublicPlayerSettings();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pridecourt.mvp");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.forceInternetPermission = true;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/MVP_Prototype.unity" },
                locationPathName = Path.Combine(outputDirectory, "PrideCourt-Android.apk"),
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            RequireSuccessfulBuild(options, "PRIDE_COURT_PUBLIC_ANDROID_BUILD_PASSED");
        }

        private static void ConfigurePublicPlayerSettings()
        {
            PlayerSettings.companyName = "Pride Court Studio";
            PlayerSettings.productName = "プライド・コート";
            PlayerSettings.bundleVersion = PublicVersion;
        }

        private static void RequireSuccessfulBuild(BuildPlayerOptions options, string successMarker)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(successMarker.Replace("PASSED", "FAILED") + ": " + report.summary.result);

            Debug.Log(successMarker + ": " + options.locationPathName);
            EditorApplication.Exit(0);
        }
    }
}
