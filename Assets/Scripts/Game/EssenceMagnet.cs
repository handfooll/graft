using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The player's pull on loose essence, and the one rule that makes a full cargo bar mean
    /// something: **at full cargo it does not attract.**
    ///
    /// That refusal is the whole of DESIGN.md §4's "at full cargo, gathering stops mattering →
    /// a natural, non-nagging prompt to head home". No warning, no banner, no interruption - the
    /// essence you are killing for simply stops coming to you and lies there instead. It is also
    /// what makes the phase's third gate image legible: a full bar, deep in Ember Scar, with motes
    /// on the ground that will not be collected.
    ///
    /// Thin on purpose. The attraction radius belongs on the player because it is a property of the
    /// player - Phase 5 has an obvious graft that widens it - while the pooling, the ticking and
    /// the drawing all belong to <see cref="EssenceField"/>, which owns the motes.
    /// </summary>
    [RequireComponent(typeof(PlayerCargo))]
    public class EssenceMagnet : MonoBehaviour
    {
        [SerializeField] EssenceTuning tuning;

        PlayerCargo _cargo;

        public EssenceTuning Tuning { get => tuning; set => tuning = value; }

        public ExtractionLedger Ledger => Cargo != null ? Cargo.Ledger : null;

        PlayerCargo Cargo => _cargo != null ? _cargo : _cargo = GetComponent<PlayerCargo>();

        /// <summary>
        /// Metres. About a third of the 9.5 m the camera shows, so essence is seen to be drawn in
        /// rather than arriving from off screen - a magnet that reached past the frame would make
        /// the reward for a kill something the player is told about instead of something they watch.
        /// </summary>
        public float Radius => tuning != null ? tuning.attractRadius : 0f;

        public float FlightSpeed => tuning != null ? tuning.flightSpeed : 9f;

        /// <summary>
        /// Whether this mote would be pulled in. False when there is no room for it, which leaves
        /// it lying on the ground - see the note on this class.
        /// </summary>
        public bool WillAttract(EssenceMote mote)
        {
            var ledger = Ledger;
            return ledger != null && ledger.HasRoomFor(mote);
        }

        public bool InRange(Vector3 point)
        {
            var offset = point - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= Radius * Radius;
        }

        /// <summary>Takes the mote. False if the bar filled while it was in flight.</summary>
        public bool Absorb(EssenceMote mote)
        {
            var ledger = Ledger;
            return ledger != null && ledger.Absorb(mote);
        }
    }
}
