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
        /// <summary>
        /// The shape the game is actually played in (D8) - an iPhone 14 Pro's 1179x2556, scaled
        /// down. **Not 9:16.** A 720x1280 capture is 22% wider in aspect than the phone, so every
        /// gate image drawn at that size showed more world than the device renders and every
        /// framing judgement made from one was made about a screen that does not exist.
        /// </summary>
        public const int Width = 720;
        public const int Height = 1560;

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

        /// <summary>
        /// A <c>ScreenSpaceOverlay</c> canvas draws straight to the display and renders **nothing**
        /// into a camera's target texture - so any HUD would be silently missing from every gate
        /// image. These switch to <c>ScreenSpaceCamera</c> for the duration of one render and go
        /// straight back, because Overlay is the cheaper path and the one the phone should run.
        /// </summary>
        static Canvas[] BorrowOverlayCanvases(Camera camera)
        {
            var borrowed = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var switched = new System.Collections.Generic.List<Canvas>();
            foreach (var canvas in borrowed)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane * 2f, 1f);
                switched.Add(canvas);
            }

            Canvas.ForceUpdateCanvases();
            return switched.ToArray();
        }

        static void ReturnOverlayCanvases(Canvas[] canvases)
        {
            foreach (var canvas in canvases)
            {
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
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

            Canvas[] canvases = null;

            try
            {
                // Target texture first: a ScreenSpaceCamera canvas sizes itself to the camera's
                // viewport, and the viewport is the texture only once the texture is assigned.
                camera.targetTexture = target;
                canvases = BorrowOverlayCanvases(camera);
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
                if (canvases != null)
                {
                    ReturnOverlayCanvases(canvases);
                }

                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
