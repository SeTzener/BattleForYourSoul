using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Fixed-location Android build entry points used by the Android Studio deploy hook.
    /// Keeping these paths stable means the Gradle task and shell script never need to guess.
    /// </summary>
    public static class AndroidDeployment
    {
        public const string ApkOutputPath = "Builds/Android/BattleForYourSoul.apk";
        public const string GradleOutputPath = "Builds/AndroidStudio_Gradle";

        private const string MenuRoot = "Tools/BattleForYourSoul/";

        [MenuItem(MenuRoot + "Build Android APK")]
        public static void BuildAndroidApk()
        {
            BuildAndroid(false, ApkOutputPath, "APK");
        }

        [MenuItem(MenuRoot + "Export Android Gradle Project")]
        public static void ExportAndroidGradleProject()
        {
            BuildAndroid(true, GradleOutputPath, "Gradle project");
        }

        private static void BuildAndroid(bool exportProject, string relativeOutput, string label)
        {
            EnsureAndroidTarget();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(projectRoot, relativeOutput);
            Directory.CreateDirectory(exportProject ? output : Path.GetDirectoryName(output));

            bool previousExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            AndroidBuildSystem previousBuildSystem = EditorUserBuildSettings.androidBuildSystem;

            try
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportProject;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

                var options = new BuildPlayerOptions
                {
                    scenes = EnabledScenes(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError(
                        "[AndroidDeployment] " + label + " build failed: " +
                        report.summary.result + " (" + report.summary.totalErrors + " errors).");
                }
                else
                {
                    Debug.Log("[AndroidDeployment] " + label + " build succeeded: " + output);
                }
            }
            finally
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportProject;
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
                EditorUserBuildSettings.androidBuildSystem = previousBuildSystem;
            }
        }

        private static void EnsureAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return;
            }

            Debug.Log("[AndroidDeployment] Switching active build target to Android.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[AndroidDeployment] Could not switch the active build target to Android.");
            }
        }

        private static string[] EnabledScenes()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }
    }
}
