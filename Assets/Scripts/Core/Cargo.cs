using System;

namespace Graft.Core
{
    /// <summary>
    /// How much you are carrying, and how much you can. DESIGN.md §4: cargo has a capacity, both
    /// essence and loot consume it, and at full cargo gathering stops mattering - which is the
    /// non-nagging prompt to head home.
    ///
    /// Immutable, for the same reason <see cref="HealthPool"/> is: a caller always has the before
    /// and the after in hand, so it cannot report an absorption without knowing what it did.
    ///
    /// <see cref="TryAdd"/> is all-or-nothing rather than clamping. Clamping silently would make a
    /// refused mote indistinguishable from an absorbed one, and the refusal is a *mechanic* - it is
    /// what makes a full bar visible as motes lying on the ground that will not come to you.
    /// </summary>
    public readonly struct Cargo
    {
        public readonly float Capacity;
        public readonly float Carried;

        public Cargo(float capacity, float carried)
        {
            Capacity = MathF.Max(1f, capacity);
            Carried = Math.Clamp(carried, 0f, Capacity);
        }

        public static Cargo Empty(float capacity) => new Cargo(capacity, 0f);

        public float Remaining => Capacity - Carried;

        /// <summary>0 to 1. What the cargo bar draws.</summary>
        public float Fraction => Carried / Capacity;

        /// <summary>
        /// Nothing more will fit, exactly. Reachable because motes are quantised - every mote in
        /// the game costs exactly <c>EssenceTuning.cargoPerMote</c>, and the capacity is a whole
        /// multiple of it - so the bar lands on the ceiling rather than stopping a mote short of
        /// it forever. If that ever stops being true this becomes a state the player can never
        /// reach, so <c>ExtractionLedgerTests</c> asserts the divisibility rather than trusting it.
        /// </summary>
        public bool IsFull => Remaining <= 0f;

        public bool HasRoomFor(float units) => units <= Remaining;

        /// <summary>
        /// Takes <paramref name="units"/> if the whole lot fits. Returns false and leaves
        /// <paramref name="result"/> as it was otherwise, so the caller decides what a refused mote
        /// does - <c>EssenceMagnet</c> declines to attract it and it stays on the ground.
        /// </summary>
        public bool TryAdd(float units, out Cargo result)
        {
            result = this;
            if (units <= 0f || !HasRoomFor(units))
            {
                return false;
            }

            result = new Cargo(Capacity, Carried + units);
            return true;
        }

        public Cargo Cleared() => new Cargo(Capacity, 0f);

        /// <summary>A bigger hold, keeping what is in it. DESIGN.md §4 makes capacity a Base upgrade.</summary>
        public Cargo WithCapacity(float capacity) => new Cargo(capacity, Carried);

        public override string ToString() => $"{Carried:0.#}/{Capacity:0.#}";
    }
}
