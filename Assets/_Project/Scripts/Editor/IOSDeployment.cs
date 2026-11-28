using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Fixed-location iOS Xcode export entry point. The output folder is deleted before each build
    /// so Xcode can never keep using an older IL2CPP project from a previous export.
    /// </summary>
    public static class IOSDeployment
    {
        public const string XcodeOutputPath = "Builds/iOS/BattleForYourSoul_Latest";

        private const string MenuRoot = "Tools/BattleForYourSoul/";

        [MenuItem(MenuRoot + "Build iOS Xcode Project")]
        public static void BuildIOSXcodeProject()
        {
            EnsureIOSTarget();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(projectRoot, XcodeOutputPath);

            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    "[IOSDeployment] Xcode project build failed: " +
                    report.summary.result + " (" + report.summary.totalErrors + " errors).");
            }
            else
            {
                Debug.Log("[IOSDeployment] Xcode project build succeeded: " + output);
            }
        }

        private static void EnsureIOSTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                return;
            }

            Debug.Log("[IOSDeployment] Switching active build target to iOS.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogError("[IOSDeployment] Could not switch the active build target to iOS.");
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
