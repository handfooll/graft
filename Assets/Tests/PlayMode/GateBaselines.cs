using System.IO;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// Approved gate images, kept in the repository, and the comparison that says whether a capture
    /// still matches the one a human said yes to.
    ///
    /// **Why this exists.** Phase 3 put a live spawn director into the World scene. Every Phase 1
    /// and Phase 2 gate image is posed in that same scene, so without care all eleven of them would
    /// have quietly acquired enemies wandering through the background - eleven approved pictures
    /// silently redecorated, with every test still green. That was caught by remembering, which is
    /// not a mechanism. This is the mechanism.
    ///
    /// It is the same failure shape as the rest of `GOTCHAS.md`: nothing errors, nothing is missing,
    /// and the picture looks perfectly normal unless you happen to know what it looked like before.
    ///
    /// **`Screenshots/` is gitignored on purpose** - every image in it is reproducible. Baselines
    /// are therefore a *separate*, committed folder. They are not build output; they are the record
    /// of what was approved, and they are worth the repository space for the same reason the
    /// approval itself is worth having.
    /// </summary>
    public static class GateBaselines
    {
        public static string Directory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "GateBaselines"));

        /// <summary>
        /// Fraction of pixels allowed to differ before a capture counts as changed.
        ///
        /// Not zero. Shadow cascades, the odd float difference in a NavMesh path and URP's own
        /// temporal wobble move a scattering of pixels between runs on the same code, and a gate
        /// that cried wolf on every run would be switched off within a day - which is worse than
        /// not having one.
        ///
        /// Deliberately tight all the same. The failures this is for are not subtle: an enemy that
        /// should not be there, a health bar that stopped rendering, a fight posed at the wrong
        /// distance. Every one of those moves whole percentages of the frame.
        /// </summary>
        public const float ChangedPixelTolerance = 0.02f;

        /// <summary>How far apart two pixels must be, per channel, to count as different at all.</summary>
        const float ChannelTolerance = 0.06f;

        public static string PathFor(string name) => Path.Combine(Directory, name + ".png");

        public static bool Exists(string name) => File.Exists(PathFor(name));

        /// <summary>
        /// Writes a capture in as the approved baseline. Called only by the explicit
        /// re-baselining pass, never by the tour - a gate that silently re-approves whatever it
        /// just rendered is not a gate at all, it is a very expensive way of writing files.
        /// </summary>
        public static void Approve(string name, string capturedPath)
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.Copy(capturedPath, PathFor(name), true);
        }

        /// <summary>
        /// Fraction of pixels that differ between a capture and its baseline, or -1 when there is
        /// no baseline to compare against.
        /// </summary>
        public static float CompareWithBaseline(string name, string capturedPath, out string detail)
        {
            detail = "";

            if (!Exists(name))
            {
                detail = $"No baseline for '{name}'. Approve one with GateBaselineTool.";
                return -1f;
            }

            var baseline = Load(PathFor(name));
            var captured = Load(capturedPath);

            try
            {
                if (baseline.width != captured.width || baseline.height != captured.height)
                {
                    detail = $"Capture is {captured.width}x{captured.height}, baseline is " +
                             $"{baseline.width}x{baseline.height}. The capture size changed, which " +
                             "moves every framing judgement ever made from these images.";
                    return 1f;
                }

                var a = baseline.GetPixels();
                var b = captured.GetPixels();
                var changed = 0;

                for (var i = 0; i < a.Length; i++)
                {
                    if (Mathf.Abs(a[i].r - b[i].r) > ChannelTolerance ||
                        Mathf.Abs(a[i].g - b[i].g) > ChannelTolerance ||
                        Mathf.Abs(a[i].b - b[i].b) > ChannelTolerance)
                    {
                        changed++;
                    }
                }

                var fraction = changed / (float)a.Length;
                detail = $"{fraction:P2} of pixels differ from the approved baseline.";
                return fraction;
            }
            finally
            {
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(captured);
            }
        }

        static Texture2D Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }
    }
}
