using System.IO;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// Renders a camera to a PNG under <c>&lt;project&gt;/Screenshots/</c>.
    ///
    /// Deliberately does NOT use <c>yield return new WaitForEndOfFrame()</c> or
    /// <c>ScreenCapture</c>: under <c>-batchmode</c> the end-of-frame yield never resumes and the
    /// run hangs until timeout with no error at all. An explicit <c>camera.Render()</c> into a
    /// RenderTexture is synchronous and works headlessly.
    ///
    /// The tour must still run WITHOUT <c>-nographics</c>, or every capture comes out empty.
    /// </summary>
    public static class Screenshot
    {
        // Portrait, 9:16 - the shape the game is actually played in (D8).
        public const int Width = 720;
        public const int Height = 1280;

        public static string Directory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));

        /// <summary>
        /// Forces the quality level iOS actually runs on. The editor defaults to the "PC" level,
        /// so without this the gate images would be judged on a render path the phone never uses -
        /// different shadows, different post. iOS-first (D8) has to mean the pictures too.
        /// </summary>
        public static void UseDeviceQualityLevel()
        {
            var levels = QualitySettings.names;
            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i] == "Mobile")
                {
                    QualitySettings.SetQualityLevel(i, true);
                    return;
                }
            }

            Debug.LogWarning("[GRAFT] No 'Mobile' quality level - capturing on " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        }

        public static string Capture(string name, Camera camera = null)
        {
            camera = camera != null ? camera : Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException($"No camera to capture '{name}' with.");
            }

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                System.IO.Directory.CreateDirectory(Directory);
                var path = Path.Combine(Directory, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log($"[GRAFT] screenshot -> {path}");
                return path;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
