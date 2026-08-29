using Graft.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Graft.UI
{
    /// <summary>
    /// The two numbers the extraction decision is actually made on: what is banked, and what the
    /// bar in your hands is currently worth.
    ///
    /// **The carried value is the one that has to be there.** A fill fraction alone cannot answer
    /// "is this worth walking home for", because a half-full bar from Ember Scar banks more than a
    /// full one from the Verge - that 4.1x is the entire point of the depth gradient (§7), and a
    /// player who cannot see it is guessing at the only decision the game asks them to make.
    ///
    /// Kept to two numbers on purpose. STATUS.md's standing note is that XP Hero's screen budget is
    /// not a target to copy: most of what fills that game's screen is gacha, battle pass, events
    /// and ads, all of which DESIGN.md §8 defers. This is what the game actually needs.
    /// </summary>
    public class BankedReadout : MonoBehaviour
    {
        [SerializeField] PlayerCargo cargo;
        [SerializeField] Text banked;
        [SerializeField] Text carried;

        [Tooltip("Shown when the bar is empty, in place of a carried value of zero. A zero is a " +
                 "number the player has to read and then discard; nothing at all is faster.")]
        [SerializeField] string emptyText = "";

        float _drawnBanked = -1f;
        float _drawnCarried = -1f;

        public PlayerCargo Cargo { get => cargo; set => cargo = value; }

        /// <summary>Assigned by <c>HudBuilder</c> so the parts are real scene references.</summary>
        public void Bind(Text bankedText, Text carriedText)
        {
            banked = bankedText;
            carried = carriedText;
        }

        void LateUpdate()
        {
            var ledger = cargo != null ? cargo.Ledger : null;
            if (ledger == null)
            {
                return;
            }

            if (banked != null && !Mathf.Approximately(_drawnBanked, ledger.BankedTotal))
            {
                _drawnBanked = ledger.BankedTotal;
                banked.text = $"BANKED  {Format(_drawnBanked)}";
            }

            if (carried == null || Mathf.Approximately(_drawnCarried, ledger.CarriedValue))
            {
                return;
            }

            _drawnCarried = ledger.CarriedValue;
            carried.text = _drawnCarried > 0f ? $"carrying  {Format(_drawnCarried)}" : emptyText;
        }

        /// <summary>
        /// Whole numbers with a thousands separator. Essence is not a quantity anyone reasons about
        /// to a decimal place, and a value that jitters in its last digit every time a mote lands
        /// draws the eye away from the fight.
        /// </summary>
        static string Format(float value) => Mathf.RoundToInt(value).ToString("N0");
    }
}
