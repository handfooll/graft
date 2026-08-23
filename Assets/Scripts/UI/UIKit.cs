using UnityEngine;
using UnityEngine.UI;
using Graft.Game;

namespace Graft.UI
{
    /// <summary>
    /// The shared design system. Every screen builds through these helpers rather than
    /// styling nodes ad hoc, so the app reads as one product instead of four separate ones.
    ///
    /// Uses the engine's built-in LegacyRuntime font deliberately: TextMeshPro needs its
    /// Essential Resources imported through the editor before it will render anything, which
    /// would make the project fail to display text on a fresh clone. The built-in font has
    /// no import step and cannot go missing.
    /// </summary>
    public static class UIKit
    {
        public const float Pad = 16f;
        public const float Gap = 10f;

        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>A full-screen canvas scaled against a 1080x1920 portrait design size.</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Portrait game: match width so the HUD keeps its proportions on any aspect.
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(string name, Transform parent, Color? color = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color ?? Palette.Panel;
            // A sprite is not optional here. An Image with type = Filled and no sprite
            // silently ignores fillAmount and draws at full extent, which turned every bar
            // in the game -- health, purity, essence, dash cooldown, mastery -- into a
            // permanently full bar. Assigning it here means no caller can forget.
            img.sprite = SpriteFactory.Square();
            return img;
        }

        public static Image Bar(string name, Transform parent, Color color)
        {
            var img = Panel(name, parent, color);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size = 32,
            TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color ?? Palette.Text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string caption,
            Color fill, System.Action onClick, int fontSize = 34)
        {
            var img = Panel(name, parent, fill);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            btn.colors = colors;

            // Dark lettering on a light fill reads better on small phone screens than
            // white knockout text, and keeps the caption legible over any fill colour.
            var label = Label(name + "_Label", img.transform, caption, fontSize,
                TextAnchor.MiddleCenter, ContrastingText(fill), FontStyle.Bold);
            Stretch(label.rectTransform, 8f);

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Color ContrastingText(Color background)
        {
            float luma = 0.2126f * background.r + 0.7152f * background.g + 0.0722f * background.b;
            return luma > 0.55f ? new Color(0.09f, 0.08f, 0.11f) : Palette.Text;
        }

        /// <summary>Stretch to fill the parent with a uniform inset.</summary>
        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Anchor to a corner/edge with an explicit size, in design-resolution units.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        /// <summary>A vertical stack that lays its children out top-down.</summary>
        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing = Gap, float padding = 0f)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = true;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing = Gap, float padding = 0f)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            h.childControlWidth = true;
            h.childControlHeight = true;
            return h;
        }

        public static LayoutElement Sized(GameObject go, float height, float width = -1f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
                le.flexibleWidth = 0f;
            }
            return le;
        }

        /// <summary>A thin rule, used to separate sections without drawing a whole panel.</summary>
        public static Image Divider(Transform parent)
        {
            var img = Panel("Divider", parent, new Color(1f, 1f, 1f, 0.10f));
            Sized(img.gameObject, 2f);
            return img;
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            return (total / 60).ToString("0") + ":" + (total % 60).ToString("00");
        }
    }
}
