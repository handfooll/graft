using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Graft.EditorTools
{
    /// <summary>
    /// Headless iOS build. Produces an Xcode project at ./build/ios which xcodebuild then
    /// compiles and installs onto a device:
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod Graft.EditorTools.BuildIOS.Build -logFile build.log
    /// </summary>
    public static class BuildIOS
    {
        const string OutputPath = "build/ios";
        const string TeamId = "6WR7SYV5L7";
        const string BundleId = "com.handfooll.graft";

        public static void Build()
        {
            ConfigureIOSPlayerSettings();

            var scenes = new[] { "Assets/Scenes/Arena.unity" };
            var full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputPath);

            // Append would try to reuse a stale Xcode project; a clean replace is more
            // predictable for a build we only ever produce from CI-style invocations.
            if (Directory.Exists(full)) Directory.Delete(full, true);
            Directory.CreateDirectory(full);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = full,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[GRAFT] iOS build {summary.result} -- {summary.totalSize} bytes, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings");
            Debug.Log("[GRAFT] Xcode project at " + full);

            if (summary.result != BuildResult.Succeeded)
            {
                // A non-zero exit is what makes a failure visible to the shell.
                EditorApplication.Exit(1);
            }
        }

        static void ConfigureIOSPlayerSettings()
        {
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.iOS, BundleId);

            // Automatic signing: Xcode mints the provisioning profile for this bundle id
            // against the team, so no profile has to be managed by hand.
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = TeamId;

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

            // iOS requires IL2CPP + ARM64; Mono is not an option on device.
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.iOS, 1); // ARM64

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.companyName = "Handfooll";
            PlayerSettings.productName = "GRAFT";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.iOS.buildNumber = "1";

            // No camera/mic/location use; declaring nothing avoids pointless privacy prompts.
            PlayerSettings.iOS.cameraUsageDescription = "";
            PlayerSettings.iOS.microphoneUsageDescription = "";
            PlayerSettings.iOS.locationUsageDescription = "";
        }
    }
}
