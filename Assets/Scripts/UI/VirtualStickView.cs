using Graft.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Graft.UI
{
    /// <summary>
    /// Draws the floating stick: a ring where the thumb landed, and a knob showing how far it has
    /// been pushed. Nothing is shown until a thumb is down, and the ring appears under the thumb
    /// rather than in a fixed corner - the stick has no home on the screen, so the HUD reserves no
    /// region for it.
    ///
    /// The ring is drawn at exactly <see cref="VirtualStickInput.TravelRadiusPixels"/>. A ring that
    /// disagrees with the input is worse than no ring: it teaches a gesture the game does not read.
    /// </summary>
    public class VirtualStickView : MonoBehaviour
    {
        [SerializeField] VirtualStickInput source;
        [SerializeField] Image ring;
        [SerializeField] Image knob;

        [Tooltip("Knob diameter as a fraction of the ring's.")]
        [Range(0.15f, 0.7f)]
        [SerializeField] float knobScale = 0.42f;

        RectTransform _rect;
        Canvas _canvas;

        /// <summary>
        /// The input this mirrors. Set to null to drive the view by hand - which is what the
        /// screenshot tour does, since <c>-batchmode</c> has no pointer to press.
        /// </summary>
        public VirtualStickInput Source
        {
            get => source;
            set => source = value;
        }

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            // The sprites are generated, not imported, so they cannot be saved into the scene -
            // they are assigned here instead. An Image with no sprite is invisible, which is
            // exactly the kind of failure that looks like "the stick code is broken".
            if (ring != null)
            {
                ring.sprite = UISprites.Ring;
            }

            if (knob != null)
            {
                knob.sprite = UISprites.Disc;
            }

            Hide();
        }

        void LateUpdate()
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                return;
            }

            var origin = source.Origin;
            if (origin.HasValue)
            {
                Show(origin.Value, source.Knob, source.TravelRadiusPixels);
            }
            else
            {
                Hide();
            }
        }

        /// <summary>Positions are in screen pixels; <paramref name="radiusPixels"/> likewise.</summary>
        public void Show(Vector2 origin, Vector2 knobPosition, float radiusPixels)
        {
            ring.gameObject.SetActive(true);
            knob.gameObject.SetActive(true);

            // Screen pixels converted through the canvas rather than assumed equal to canvas units.
            // The canvas scales itself to the device's physical size, so on the phone one unit is
            // not one pixel and a ring sized in raw pixels would come out a third of the size.
            var diameter = 2f * radiusPixels / CanvasScale;
            ring.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            knob.rectTransform.sizeDelta = new Vector2(diameter * knobScale, diameter * knobScale);

            ring.rectTransform.anchoredPosition = ToCanvas(origin);
            knob.rectTransform.anchoredPosition = ToCanvas(knobPosition);
        }

        public void Hide()
        {
            if (ring != null)
            {
                ring.gameObject.SetActive(false);
            }

            if (knob != null)
            {
                knob.gameObject.SetActive(false);
            }
        }

        float CanvasScale
        {
            get
            {
                var canvas = _canvas != null ? _canvas : _canvas = GetComponentInParent<Canvas>();
                return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            }
        }

        Vector2 ToCanvas(Vector2 screenPoint)
        {
            var rect = _rect != null ? _rect : _rect = GetComponent<RectTransform>();
            var canvas = _canvas != null ? _canvas : _canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, camera, out var local);
            return local;
        }

        /// <summary>
        /// Builds the two images this view drives. Called by the editor scene builder, so the saved
        /// scene owns real objects; only the sprites on them are made at runtime.
        /// </summary>
        public void CreateParts(Color ringColour, Color knobColour)
        {
            ring = CreatePart("Ring", ringColour);
            knob = CreatePart("Knob", knobColour);
        }

        Image CreatePart(string name, Color colour)
        {
            var part = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = part.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;

            var image = part.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }
    }
}
