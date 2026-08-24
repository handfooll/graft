using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Graft.Editor
{
    /// <summary>
    /// Exports the Xcode project. Driven by <c>tools/deploy_ios.sh</c>, which then builds, installs
    /// and launches it; this half only knows about Unity.
    ///
    /// <code>
    /// Unity -batchmode -nographics -quit -projectPath . \
    ///   -executeMethod Graft.Editor.IOSBuild.Export -graftOutput &lt;dir&gt; -graftTeam &lt;id&gt;
    /// </code>
    ///
    /// **The output directory must not be on a cloud-synced volume.** <c>~/Desktop</c> here is
    /// FileProvider-backed and synthesises a <c>com.apple.FinderInfo</c> attribute on every bundle
    /// directory that cannot be removed - <c>xattr -d</c> answers "No such xattr" while
    /// <c>xattr -l</c> still lists it - and codesign then fails permanently with "resource fork,
    /// Finder information, or similar detritus not allowed". The default below is under
    /// <c>~/Library/Caches</c>, where that does not happen.
    /// </summary>
    public static class IOSBuild
    {
        public const string OutputArgument = "-graftOutput";
        public const string TeamArgument = "-graftTeam";

        public static string DefaultOutput => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/Caches/graft-ios/Xcode");

        public static void Export()
        {
            var output = Argument(OutputArgument) ?? DefaultOutput;
            var team = Argument(TeamArgument);

            if (!string.IsNullOrEmpty(team))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = team;
            }

            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ProjectSetup.BundleIdentifier);

            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                // Release, not development. The build exists to answer whether moving around feels
                // good, and a development player answers that question about a different build.
                options = BuildOptions.None
            };

            if (options.scenes.Length == 0)
            {
                throw new BuildFailedException("No scenes are enabled in build settings.");
            }

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"iOS export {summary.result}: {summary.totalErrors} errors.");
            }

            Debug.Log($"[GRAFT] Xcode project exported to {output} " +
                      $"({summary.totalTime.TotalSeconds:0} s, team '{PlayerSettings.iOS.appleDeveloperTeamID}').");
        }

        /// <summary>
        /// ProMotion is opt-in. Without <c>CADisableMinimumFrameDuration</c> iOS caps the app at
        /// 60 Hz no matter what <c>Application.targetFrameRate</c> says, and nothing reports it.
        /// </summary>
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
#if UNITY_IOS
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("CADisableMinimumFrameDuration", true);
            plist.WriteToFile(plistPath);
            Debug.Log("[GRAFT] Info.plist: CADisableMinimumFrameDuration set, ProMotion unlocked.");
#endif
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
