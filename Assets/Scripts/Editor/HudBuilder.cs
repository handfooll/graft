using Graft.Game;
using Graft.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Graft.Editor
{
    /// <summary>
    /// Builds the HUD canvas into the World scene.
    ///
    /// Phase 1 put exactly one thing on it - the floating stick - and established where everything
    /// later goes. Phase 4 fills in the part that was reserved: the cargo bar and the two numbers
    /// beside it live under <c>SafeArea</c>, while the stick deliberately still does not, because
    /// it appears wherever the thumb lands and a ring nudged inside a safe-area inset no longer
    /// sits under the finger that drew it.
    ///
    /// **The whole HUD is three lines of information.** STATUS.md's standing note is that XP Hero's
    /// screen budget is not a target to copy - most of what fills that game's screen is gacha,
    /// battle pass, events and ads, all of which DESIGN.md §8 defers. What is left is what the game
    /// actually needs to be playable: how full you are, what it is worth, and what you have already
    /// made safe.
    /// </summary>
    public static class HudBuilder
    {
        /// <summary>
        /// Affects how sprites map to canvas units, and nothing else. It does **not** set the size
        /// of a canvas unit - see the note on <see cref="PhysicalUnit"/>.
        /// </summary>
        public const float DefaultSpriteDpi = 96f;

        /// <summary>Used when the device reports no DPI. Roughly an iPhone's logical density.</summary>
        public const float FallbackDpi = 326f;

        /// <summary>
        /// **One canvas unit is one point - 1/72 inch.** The HUD is authored in physical units,
        /// because a thumb is not measured in pixels and an inset picked against one iPhone is
        /// wrong on the rest; the point is the physical unit that UI has always been written in.
        ///
        /// It used to be <c>Centimeters</c>, and that was a mistake worth naming. Under
        /// <c>ConstantPhysicalSize</c> the physical unit *is* the canvas unit, so the whole canvas
        /// was about six units across - and <c>Text.fontSize</c> is an integer, which makes every
        /// legible type size on such a canvas either 0 or the height of the screen. Phase 1 never
        /// noticed because the only thing on the canvas was the floating stick, which sizes itself
        /// from screen pixels through <c>canvas.scaleFactor</c> at runtime and so is immune.
        /// Phase 4's cargo panel, authored in tens of units, came out at **negative width** and
        /// simply did not render. See GOTCHAS.md.
        /// </summary>
        public const CanvasScaler.Unit PhysicalUnit = CanvasScaler.Unit.Points;

        /// <summary>
        /// Everything below is in points. A phone screen is about 180 of them across - an iPhone
        /// 14 Pro is 184 - so the panel spans 160 with a 10-point margin each side, and stands 42
        /// tall: a 12-point bar with a line of type above and below it.
        /// </summary>
        const float PanelHeight = 42f;
        const float SideMargin = 10f;
        const float BarHeight = 12f;
        const float LabelHeight = 15f;
        const float BorderWidth = 1.5f;

        /// <summary>
        /// The one bit of art in the HUD that is not generated: Unity's built-in font.
        ///
        /// Referenced as an asset at build time rather than fetched at runtime, exactly as the
        /// combat materials are, and for the same reason - an iOS build strips what nothing in a
        /// scene points at, and a font resolved at runtime on the device is a HUD with no text in
        /// it and no error to say so.
        /// </summary>
        const string BuiltinFont = "LegacyRuntime.ttf";

        public static GameObject Build(VirtualStickInput stick, PlayerCargo cargo)
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
            scaler.physicalUnit = PhysicalUnit;
            scaler.fallbackScreenDPI = FallbackDpi;
            scaler.defaultSpriteDPI = DefaultSpriteDpi;

            var safe = BuildSafeArea(root.transform);
            BuildCargoPanel(safe, cargo);
            BuildStick(root.transform, stick);
            return root;
        }

        /// <summary>
        /// The frame every HUD widget hangs off. Built empty in Phase 1 specifically so nothing
        /// added later would have to guess an inset for the Dynamic Island; Phase 4 is what fills it.
        /// </summary>
        static Transform BuildSafeArea(Transform parent)
        {
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
            Stretch(safe.GetComponent<RectTransform>(), parent);
            return safe.transform;
        }

        /// <summary>
        /// Cargo, at the top of the safe area:
        ///
        /// <code>
        /// CARGO                        BANKED  4,210
        /// [============------------------------]
        /// carrying  1,180
        /// </code>
        ///
        /// Top rather than bottom, and that is not arbitrary. The bottom third of a portrait phone
        /// is where the thumb lives and where the floating stick draws itself; anything parked
        /// there is under a hand for the half of the game that involves moving. The top is the only
        /// region of the screen that is never occluded.
        /// </summary>
        static void BuildCargoPanel(Transform parent, PlayerCargo cargo)
        {
            var font = Resources.GetBuiltinResource<Font>(BuiltinFont);

            var panel = new GameObject("Cargo", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.offsetMin = new Vector2(SideMargin, -PanelHeight);
            panel.offsetMax = new Vector2(-SideMargin, 0f);

            var label = Label(panel, "Label", font, TextAnchor.UpperLeft, 11,
                left: 0f, right: 0.5f, top: 0f, bottom: LabelHeight);
            label.text = "CARGO";
            label.color = new Color(1f, 1f, 1f, 0.72f);

            var banked = Label(panel, "Banked", font, TextAnchor.UpperRight, 11,
                left: 0.5f, right: 1f, top: 0f, bottom: LabelHeight);
            banked.text = "BANKED  0";
            banked.color = new Color(1f, 0.93f, 0.78f, 0.9f);

            // The bar itself, between the two text rows.
            var bar = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(panel, false);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -(LabelHeight + BarHeight));
            bar.offsetMax = new Vector2(0f, -LabelHeight);

            var track = Panel(bar, "Track", new Color(0.05f, 0.05f, 0.07f, 0.62f));
            var fill = Panel(bar, "Fill", new Color(1f, 0.78f, 0.32f, 0.95f));
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);

            // The full-cargo border. Four hairlines rather than an outline component, because an
            // Image cannot draw a stroke and a generated ring sprite would be the wrong shape for a
            // rectangle. Off until the bar is full; switched on as one object.
            var border = BuildBorder(bar);

            var carried = Label(panel, "Carried", font, TextAnchor.UpperLeft, 10,
                left: 0f, right: 1f, top: LabelHeight + BarHeight, bottom: LabelHeight + BarHeight + LabelHeight);
            carried.text = "";
            carried.color = new Color(1f, 0.86f, 0.55f, 0.92f);

            var cargoBar = panel.gameObject.AddComponent<CargoBar>();
            cargoBar.Cargo = cargo;
            cargoBar.Bind(track, fill, border, label);

            var readout = panel.gameObject.AddComponent<BankedReadout>();
            readout.Cargo = cargo;
            readout.Bind(banked, carried);
        }

        /// <summary>
        /// The full-cargo border: four hairlines around the bar, as one object to switch on.
        ///
        /// The group itself is an empty rect stretched over the whole bar, and the four edges are
        /// its children. It has to be that way round - an edge anchored inside a group that is
        /// itself only a hairline tall would be positioned against the hairline rather than against
        /// the bar, and the rectangle would collapse into a single line at the top with nothing to
        /// report it.
        /// </summary>
        static GameObject BuildBorder(RectTransform parent)
        {
            var group = new GameObject("Border", typeof(RectTransform));
            Stretch(group.GetComponent<RectTransform>(), parent);

            Edge(group.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -BorderWidth), Vector2.zero);
            Edge(group.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, BorderWidth));
            Edge(group.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(BorderWidth, 0f));
            Edge(group.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-BorderWidth, 0f), Vector2.zero);

            group.SetActive(false);
            return group;
        }

        static void Edge(Transform group, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = edge.GetComponent<RectTransform>();
            rect.SetParent(group, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = edge.GetComponent<Image>();
            // Deep amber, not the pale of the fill it surrounds. A cream border around a cream
            // full bar is invisible, which defeats the point of drawing one.
            image.color = new Color(0.86f, 0.55f, 0.12f, 1f);
            image.raycastTarget = false;
        }

        static Image Panel(RectTransform parent, string name, Color colour)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = panel.GetComponent<RectTransform>();
            Stretch(rect, parent);

            var image = panel.GetComponent<Image>();
            image.color = colour;
            // No sprite: a plain Image with none draws a solid rectangle, which is exactly what a
            // bar is. The generated sprites in UISprites exist for the circles the stick needs.
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// One line of HUD text. <paramref name="left"/> and <paramref name="right"/> are fractions
        /// of the panel's width; <paramref name="top"/> and <paramref name="bottom"/> are canvas
        /// units *down* from the panel's top edge, which is the direction the panel is laid out in.
        /// </summary>
        static Text Label(RectTransform parent, string name, Font font, TextAnchor alignment, int size,
            float left, float right, float top, float bottom)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = label.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, 1f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -bottom);
            rect.offsetMax = new Vector2(0f, -top);

            var text = label.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            // Off. Best Fit resizes text to whatever fits, which would silently make the banked
            // total shrink as it grew - the one number in the HUD that should get harder to ignore.
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // A hard dark shadow, because the HUD floats directly on the world with no backing
            // plate and the world is six different colours. Pale type on Bone Reach's bone-coloured
            // ground is the worst case and it is legible rather than comfortable; §4 asks for
            // readable at a glance, in a fight. A scrim behind the panel would fix it too and would
            // cost a permanent grey slab across the top of every screen in the game.
            var shadow = label.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
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
