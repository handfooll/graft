using Graft.Game;
using Graft.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Graft.Editor
{
    /// <summary>
    /// Builds the HUD canvas into the World scene.
    ///
    /// Phase 1 puts exactly one thing on it - the floating stick - and establishes where everything
    /// later goes. The cargo bar (DESIGN.md §4, Phase 4) belongs under <c>SafeArea</c>; the stick
    /// deliberately does not, because it appears wherever the thumb lands and a ring that has been
    /// nudged inside a safe-area inset no longer sits under the finger that drew it.
    /// </summary>
    public static class HudBuilder
    {
        /// <summary>
        /// Canvas units per centimetre of real screen. <c>ConstantPhysicalSize</c> with a
        /// <c>defaultSpriteDPI</c> of 96 makes one canvas unit 1/96 inch, so the whole HUD is
        /// authored in physical units - which is the only sizing that means the same thing on
        /// every iPhone. A thumb is not measured in pixels.
        /// </summary>
        public const float DefaultSpriteDpi = 96f;

        /// <summary>Used when the device reports no DPI. Roughly an iPhone's logical density.</summary>
        public const float FallbackDpi = 326f;

        public static GameObject Build(VirtualStickInput stick)
        {
            var root = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            // Overlay is the cheap path and the one the phone runs. Captures switch it to
            // ScreenSpaceCamera for the duration of a render, because an overlay canvas draws
            // nothing at all into a camera's target texture - see Screenshot.Capture.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            scaler.physicalUnit = CanvasScaler.Unit.Centimeters;
            scaler.fallbackScreenDPI = FallbackDpi;
            scaler.defaultSpriteDPI = DefaultSpriteDpi;

            BuildSafeArea(root.transform);
            BuildStick(root.transform, stick);
            return root;
        }

        /// <summary>
        /// The frame every later HUD widget hangs off. Empty in Phase 1 on purpose - it exists so
        /// that nothing added in Phase 4 has to guess an inset for the Dynamic Island.
        /// </summary>
        static void BuildSafeArea(Transform parent)
        {
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
            Stretch(safe.GetComponent<RectTransform>(), parent);
        }

        static void BuildStick(Transform parent, VirtualStickInput stick)
        {
            var layer = new GameObject("VirtualStick", typeof(RectTransform), typeof(VirtualStickView));
            Stretch(layer.GetComponent<RectTransform>(), parent);

            var view = layer.GetComponent<VirtualStickView>();
            view.Source = stick;
            // Translucent: the stick sits on top of the ground the player is walking over, and an
            // opaque one would hide the half of the screen the thumb is already covering.
            view.CreateParts(
                ringColour: new Color(1f, 1f, 1f, 0.28f),
                knobColour: new Color(1f, 1f, 1f, 0.55f));
        }

        static void Stretch(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // A fresh RectTransform carries sizeDelta (100,100); stretch anchors alone would leave
            // it 100 units larger than its parent in both directions.
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
