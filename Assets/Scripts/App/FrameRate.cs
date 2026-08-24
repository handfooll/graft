using UnityEngine;

namespace Graft.App
{
    /// <summary>
    /// Unlocks the frame rate on the device.
    ///
    /// **Unity defaults iOS to 30 fps.** Nothing warns about it, and it is fatal to judging Phase 1:
    /// movement tuned against a 30 fps build is being judged through 33 ms of input latency that
    /// the shipped game would not have. The build's whole purpose is answering "does this feel
    /// right", so the frame rate has to be the real one before the question means anything.
    ///
    /// Matches the display rather than assuming 60 - the iPhone this is being tested on runs
    /// ProMotion at 120 Hz, and that needs <c>CADisableMinimumFrameDuration</c> in Info.plist as
    /// well, which the iOS build post-process sets.
    /// </summary>
    public static class FrameRate
    {
        public const int Minimum = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            // VSync overrides targetFrameRate wherever it is honoured, so it has to go first.
            QualitySettings.vSyncCount = 0;

            var refreshRate = (float)Screen.currentResolution.refreshRateRatio.value;
            var target = refreshRate > 1f ? Mathf.RoundToInt(refreshRate) : Minimum;
            Application.targetFrameRate = Mathf.Max(Minimum, target);

            // A game played with a thumb held still on a stick still counts as idle to iOS.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
