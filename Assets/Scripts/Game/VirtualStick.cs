using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Graft.Game
{
    /// <summary>
    /// A floating thumb stick: it appears wherever the thumb lands in its region rather
    /// than at a fixed spot, which is what makes one-thumb play comfortable without asking
    /// the player to look down at the controls.
    /// Lives in the Game assembly because <see cref="PlayerController"/> reads it directly.
    /// </summary>
    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public bool IsActive { get; private set; }
        public Vector2 Value { get; private set; }

        RectTransform _self;
        RectTransform _ring;
        RectTransform _knob;
        Canvas _canvas;
        Vector2 _origin;
        float _radius = 120f;

        public static VirtualStick Create(RectTransform parent, Canvas canvas)
        {
            var go = new GameObject("VirtualStick", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            // Covers the lower two-thirds of the screen so the thumb can start anywhere
            // natural, while leaving the top clear for HUD taps.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.62f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);   // invisible, but must still raycast
            img.raycastTarget = true;

            var stick = go.AddComponent<VirtualStick>();
            stick._self = rt;
            stick._canvas = canvas;

            stick._ring = MakeGraphic(rt, "Ring", 240f, new Color(1f, 1f, 1f, 0.13f));
            stick._knob = MakeGraphic(rt, "Knob", 96f, new Color(1f, 1f, 1f, 0.30f));
            stick.SetVisible(false);
            return stick;
        }

        static RectTransform MakeGraphic(RectTransform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = SpriteFactory.Circle();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        void SetVisible(bool visible)
        {
            _ring.gameObject.SetActive(visible);
            _knob.gameObject.SetActive(visible);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!ToLocal(e, out _origin)) return;
            IsActive = true;
            SetVisible(true);
            _ring.anchoredPosition = _origin;
            _knob.anchoredPosition = _origin;
            Value = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!IsActive) return;
            if (!ToLocal(e, out var current)) return;

            Vector2 delta = current - _origin;
            float mag = delta.magnitude;
            if (mag > _radius) delta = delta / mag * _radius;

            _knob.anchoredPosition = _origin + delta;
            Value = delta / _radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            IsActive = false;
            Value = Vector2.zero;
            SetVisible(false);
        }

        bool ToLocal(PointerEventData e, out Vector2 local)
        {
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _self, e.position, cam, out local);
        }
    }
}
