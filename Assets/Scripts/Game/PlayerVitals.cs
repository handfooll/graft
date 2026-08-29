using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The player's health, and the death rule.
    ///
    /// Phase 3 left <see cref="Fall"/> as a stand-in - back to Base, whole, nothing lost, because
    /// there was nothing yet to lose - and said Phase 4 would replace it. This is that replacement,
    /// and the seam held: it is one method, and combat did not have to be reopened to change it.
    ///
    /// The rule is D4 read literally. **All** unbanked cargo, and nothing else, ever. Its "only"
    /// limits which categories are at risk, not what fraction of them - and keeping some fraction
    /// back would blunt exactly the thing DESIGN.md §3 calls the whole game. There is no corpse run
    /// and no recovery: not because either is forbidden, but because Phase 4 exists to measure
    /// whether the walk home is a commitment, and a recovery mechanic dulls the signal while the
    /// measurement is being taken. It is additive later, and it is the **first lever** if the death
    /// cost turns out to be too harsh - before cargo capacity or essence value, which the depth
    /// gradient leans on.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] CombatTuning tuning;
        [SerializeField] WorldRegions regions;
        [SerializeField] PlayerCargo cargo;
        [SerializeField] BankZone bank;

        Damageable _health;
        HealthBar _bar;
        PlayerController _controller;

        public CombatTuning Tuning { get => tuning; set => tuning = value; }
        public WorldRegions Regions { get => regions; set => regions = value; }
        public PlayerCargo Cargo { get => cargo; set => cargo = value; }
        public BankZone Bank { get => bank; set => bank = value; }

        public Damageable Health => _health;

        /// <summary>How many times the player has been put back at Base. Read by the tour.</summary>
        public int Falls { get; private set; }

        /// <summary>Value of the cargo the last death cost. Zero if nothing was being carried.</summary>
        public float LastLoss { get; private set; }

        void Awake()
        {
            _health = GetComponent<Damageable>();
            _controller = GetComponent<PlayerController>();

            _bar = GetComponent<HealthBar>();
            if (_bar == null)
            {
                _bar = gameObject.AddComponent<HealthBar>();
            }
        }

        void Start()
        {
            if (tuning == null)
            {
                return;
            }

            _health.ResetTo(tuning.playerMaxHealth);
            _health.Died += OnDied;

            // The player carries the same world-space bar the enemies do. Under a fixed camera it
            // costs nothing, and it means the one number the player has to watch is in the same
            // place as everything they are watching - rather than in a corner of the screen the
            // HUD has not been designed for yet.
            _bar.Bind(_health, tuning, PlayerBarHeight());
        }

        void Update()
        {
            if (tuning == null || regions == null || !_health.IsAlive)
            {
                return;
            }

            // Base heals. §7 makes it the safe hub, and this is the cheapest possible statement of
            // that - Phase 4 replaces it with banking, which is the version that costs something.
            if (tuning.baseHealSeconds > 0f && regions.IsInBase(transform.position))
            {
                _health.Heal(_health.Pool.Maximum / tuning.baseHealSeconds * Time.deltaTime);
            }
        }

        void OnDied(Damageable damageable) => Fall();

        /// <summary>
        /// Death out in the world: lose every unbanked unit, respawn at Base, keep everything that
        /// was already banked (D4, §3).
        ///
        /// The order matters. The cargo is lost **before** the teleport, so the loss is recorded
        /// against where the player actually died rather than against Base - and so that the ledger
        /// is already empty by the time <see cref="BankZone"/> sees the player arrive on the plaza.
        /// Then the bank zone is resynced, because being carried home dead is not banking: without
        /// that line, dying would fire the event Phase 5 clears grafts and resets corruption on,
        /// which is a different rule from the one §3 draws.
        /// </summary>
        void Fall()
        {
            Falls++;
            LastLoss = 0f;

            var ledger = cargo != null ? cargo.Ledger : null;
            if (ledger != null)
            {
                LastLoss = ledger.LoseCarried();
            }

            var home = regions != null ? regions.AnchorFor(Area.Base) : Vector3.zero;
            if (_controller != null)
            {
                _controller.Teleport(home, 0f);
            }
            else
            {
                transform.position = home;
            }

            _health.ResetTo(tuning != null ? tuning.playerMaxHealth : 100f);

            if (bank != null)
            {
                bank.Resync();
            }

            var banked = ledger != null ? ledger.BankedTotal : 0f;
            Debug.Log($"[GRAFT] Player died carrying {LastLoss:0.#} in essence - all of it lost. " +
                      $"Banked total is untouched at {banked:0.#}.");
        }

        /// <summary>
        /// Where to hang the bar. Read off the CharacterController rather than the proportions
        /// asset, because the controller is what was actually sized to the silhouette and this way
        /// there is one height in play instead of two that can disagree.
        /// </summary>
        float PlayerBarHeight()
        {
            var controller = GetComponent<CharacterController>();
            return controller != null ? controller.height : 1.5f;
        }
    }
}
