using Graft.Core;
using Graft.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Graft.UI
{
    /// <summary>
    /// The cargo bar. DESIGN.md §4: "cargo is visible at all times (a bar), because the player must
    /// be able to judge the risk."
    ///
    /// It hangs off <c>SafeArea</c>, which Phase 1 built empty for exactly this - every iPhone
    /// insets differently and a margin picked against one is wrong on the rest.
    ///
    /// **The full state is a different picture, not a brighter one.** At capacity the fill goes
    /// pale, a border appears around the whole bar and the label changes word. That matters more
    /// than it sounds: full cargo is the moment §4 makes the game's only prompt to go home, and it
    /// has to survive being read at a glance, in a fight, on a phone, by someone who cannot
    /// separate gold from amber. Shape and text carry it; colour only reinforces.
    ///
    /// Redrawn only when the numbers change. A <c>Text</c> assigned every frame dirties the canvas
    /// every frame, which on the device is a full UI rebuild sixty times a second to show a number
    /// that did not move.
    /// </summary>
    public class CargoBar : MonoBehaviour
    {
        [SerializeField] PlayerCargo cargo;
        [SerializeField] Image track;
        [SerializeField] Image fill;

        [Tooltip("The four-hairline rectangle drawn round the bar at capacity. A whole object " +
                 "rather than an Image, because it is four of them.")]
        [SerializeField] GameObject border;

        [SerializeField] Text label;

        [Header("Colours")]
        [Tooltip("Essence gold. Deliberately nothing like the health bars' red or the stick's " +
                 "white - three HUD elements that mean different things should not share a hue.")]
        [SerializeField] Color fillColour = new Color(1f, 0.78f, 0.32f, 0.95f);

        [Tooltip("Shown at capacity. Pale rather than merely brighter, so the change reads as a " +
                 "state and not as a highlight.")]
        [SerializeField] Color fullColour = new Color(1f, 0.96f, 0.86f, 1f);

        float _drawn = -1f;
        bool _drawnFull;
        bool _everDrawn;

        public PlayerCargo Cargo { get => cargo; set => cargo = value; }

        /// <summary>The fraction currently drawn. Read by the tour, which photographs it.</summary>
        public float Shown => _drawn;

        /// <summary>Whether the bar is in its full state right now.</summary>
        public bool ShowingFull => _drawnFull;

        /// <summary>Assigned by <c>HudBuilder</c> so the parts are real scene references.</summary>
        public void Bind(Image trackImage, Image fillImage, GameObject borderObject, Text labelText)
        {
            track = trackImage;
            fill = fillImage;
            border = borderObject;
            label = labelText;
        }

        void LateUpdate()
        {
            var ledger = cargo != null ? cargo.Ledger : null;
            if (ledger == null || fill == null)
            {
                return;
            }

            Redraw(ledger.Carried);
        }

        void Redraw(Cargo carried)
        {
            var fraction = Mathf.Clamp01(carried.Fraction);
            var full = carried.IsFull;

            // A hundredth of the bar is under a pixel on the device; redrawing for less than that
            // is a canvas rebuild nobody can see.
            if (_everDrawn && full == _drawnFull && Mathf.Abs(fraction - _drawn) < 0.005f)
            {
                return;
            }

            _drawn = fraction;
            _drawnFull = full;
            _everDrawn = true;

            // Anchors rather than sizeDelta, so the fill is a fraction of whatever width the safe
            // area turned out to be on this device instead of a pixel count measured on one phone.
            var rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(fraction, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            fill.color = full ? fullColour : fillColour;

            if (track != null)
            {
                // Darkened under a full bar. The pale fill is nearly white and needs something to
                // be pale *against*, or a full bar and a bar with a bright fill look the same.
                track.color = full
                    ? new Color(0.02f, 0.02f, 0.03f, 0.85f)
                    : new Color(0.05f, 0.05f, 0.07f, 0.62f);
            }

            if (border != null)
            {
                border.SetActive(full);
            }

            if (label != null)
            {
                label.text = full ? "CARGO FULL" : "CARGO";
                label.color = full ? fullColour : new Color(1f, 1f, 1f, 0.72f);
            }
        }
    }
}
