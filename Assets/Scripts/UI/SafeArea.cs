using UnityEngine;

namespace Graft.UI
{
    /// <summary>
    /// Insets a RectTransform to <see cref="Screen.safeArea"/>, so nothing in the HUD lands under
    /// the Dynamic Island or the home indicator.
    ///
    /// Applied at runtime from the device's own reported safe area rather than from hardcoded
    /// margins: the inset differs between every iPhone, and a margin picked against one of them is
    /// wrong everywhere else. Re-applies when the resolution changes, which is also what an
    /// orientation change looks like.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied;
        Vector2Int _appliedResolution;

        void Awake() => _rect = GetComponent<RectTransform>();

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != _applied ||
                _appliedResolution.x != Screen.width ||
                _appliedResolution.y != Screen.height)
            {
                Apply();
            }
        }

        void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            _applied = Screen.safeArea;
            _appliedResolution = new Vector2Int(Screen.width, Screen.height);

            var min = new Vector2(_applied.xMin / Screen.width, _applied.yMin / Screen.height);
            var max = new Vector2(_applied.xMax / Screen.width, _applied.yMax / Screen.height);

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            // A fresh RectTransform carries sizeDelta (100,100). Stretch anchors alone leave it 100
            // units wider than its parent, clipping both edges - this is the line that stops that.
            _rect.sizeDelta = Vector2.zero;
            _rect.anchoredPosition = Vector2.zero;
        }
    }
}
