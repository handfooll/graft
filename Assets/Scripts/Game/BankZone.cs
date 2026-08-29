using System;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The 7 m plaza at the monument. Walking into it banks everything, with no button press
    /// (D-P4.2, DESIGN.md §3).
    ///
    /// **Fires on the crossing in, not while standing there.** <see cref="ExtractionLedger.Bank"/>
    /// raises its event unconditionally - banking is an act, and D5 makes it what resets corruption
    /// even for a player who walked home carrying nothing - so it is this component's job to call
    /// it once per arrival rather than sixty times a second. Getting that wrong would spam Phase 5
    /// with resets and drown the payoff the moment it exists.
    ///
    /// Asked of <see cref="WorldRegions"/> rather than done with a trigger collider. A collider
    /// would be a second copy of where the plaza is, free to drift away from the paving that was
    /// generated from the layout - and it would need a rigidbody on the player to fire at all,
    /// which is exactly the physics the CharacterController was chosen to avoid.
    /// </summary>
    public class BankZone : MonoBehaviour
    {
        [SerializeField] WorldRegions regions;
        [SerializeField] PlayerCargo cargo;

        bool _inside;

        public WorldRegions Regions { get => regions; set => regions = value; }
        public PlayerCargo Cargo { get => cargo; set => cargo = value; }

        /// <summary>
        /// Raised on each arrival, after the ledger has banked. The HUD listens to show the total
        /// jumping; Phase 5 will listen through <see cref="ExtractionLedger.Banked"/> instead,
        /// which is the seam that survives this component being rewritten.
        /// </summary>
        public event Action<BankReceipt> Banked;

        /// <summary>How many times cargo has been banked this session. Read by the tour.</summary>
        public int Bankings { get; private set; }

        /// <summary>Standing on the plaza right now.</summary>
        public bool PlayerIsHome => _inside;

        /// <summary>
        /// Adopts wherever the player currently is as the starting state, without banking.
        ///
        /// Called on load, because an expedition begins standing on the plaza and a bank on frame
        /// one would be a payoff for nothing - and called again after a death, because death
        /// teleports the player home and *death is not banking*. Without this, dying out in Ember
        /// Scar would land the player on the plaza and immediately fire the banking event that
        /// Phase 5 clears grafts and resets corruption on, which is a materially different rule
        /// from the one DESIGN.md §3 draws.
        /// </summary>
        public void Resync()
        {
            _inside = regions != null && cargo != null && regions.IsInBankPlaza(cargo.transform.position);
        }

        void Start() => Resync();

        void Update()
        {
            if (regions == null || cargo == null)
            {
                return;
            }

            var inside = regions.IsInBankPlaza(cargo.transform.position);
            if (inside == _inside)
            {
                return;
            }

            _inside = inside;
            if (!inside)
            {
                return;
            }

            var ledger = cargo.Ledger;
            if (ledger == null)
            {
                return;
            }

            var receipt = ledger.Bank();
            Bankings++;
            Banked?.Invoke(receipt);

            if (!receipt.WasEmpty)
            {
                Debug.Log($"[GRAFT] Banked at the monument: {receipt}.");
            }
        }
    }
}
