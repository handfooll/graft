using System;

namespace Graft.Core
{
    /// <summary>
    /// The extraction loop, as arithmetic. DESIGN.md §3 in one type: absorb essence into cargo,
    /// bank it at Base and keep it forever, or die out there and lose every unbanked unit (D4).
    ///
    /// **This is where Phase 4 actually lives.** Everything in <c>Graft.Game</c> is presentation of
    /// what happens here - the motes flying in, the bar filling, the plaza trigger. If this
    /// arithmetic is wrong, no amount of HUD makes the loop read.
    ///
    /// The two events are deliberately the seam Phase 5 hangs off. Banking is where corruption
    /// resets and grafts clear (D5, §3), and none of that exists yet - so Phase 4 raises the event
    /// and builds nothing behind it.
    ///
    /// One rename against the spec worth knowing: the spec calls the banked total <c>Banked</c>,
    /// but C# cannot give a class a property and an event of the same name. The **event** keeps the
    /// name, because that is what Phase 5 subscribes to; the total is <see cref="BankedTotal"/>.
    /// </summary>
    public sealed class ExtractionLedger
    {
        readonly EssenceValues _values;

        Cargo _carried;
        float _carriedValue;

        public ExtractionLedger(EssenceValues values, float capacity)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
            _carried = Cargo.Empty(capacity);
        }

        /// <summary>
        /// Banking. Raised once per <see cref="Bank"/>, carrying what was banked and the new total.
        /// Phase 5 resets corruption and clears grafts from here (D5).
        /// </summary>
        public event Action<BankReceipt> Banked;

        /// <summary>
        /// Dying out in the world. Carries the value that was lost, so the HUD can say what it
        /// cost - a number the player never sees is not a stake.
        /// </summary>
        public event Action<float> Lost;

        /// <summary>Raised whenever the carried cargo changes, for whatever reason.</summary>
        public event Action<Cargo> CarriedChanged;

        public Cargo Carried => _carried;

        /// <summary>Permanent. Nothing in the game reduces this - D4 puts only unbanked cargo at risk.</summary>
        public float BankedTotal { get; private set; }

        /// <summary>
        /// What the bar in your hands would bank for right now. **The number the extraction
        /// decision is made on**, which is why the HUD shows it and not just the fill fraction: a
        /// half-full bar from Ember Scar is worth more than a full one from the Verge, and a player
        /// who cannot see that is guessing.
        /// </summary>
        public float CarriedValue => _carriedValue;

        public bool IsFull => _carried.IsFull;

        public bool HasRoomFor(EssenceMote mote) => _carried.HasRoomFor(mote.CargoUnits);

        /// <summary>
        /// Takes a mote in, if it fits whole. Returns false otherwise and changes nothing -
        /// the caller's cue to leave it lying on the ground.
        /// </summary>
        public bool Absorb(EssenceMote mote)
        {
            if (!_carried.TryAdd(mote.CargoUnits, out var next))
            {
                return false;
            }

            _carried = next;
            _carriedValue += _values.ValueOf(mote);
            CarriedChanged?.Invoke(_carried);
            return true;
        }

        /// <summary>
        /// Everything carried becomes permanent, and the hold is empty again.
        ///
        /// Raises <see cref="Banked"/> unconditionally, including on an empty bar. Banking is an
        /// act rather than a transfer: D5 makes it what resets corruption, and a player who
        /// retreats to Base carrying nothing has still ended their expedition. Firing only on a
        /// non-empty haul would mean Phase 5's reset silently skipped exactly the case where the
        /// player most needs it - walking home with 90% corruption and nothing to show for it.
        ///
        /// Which means the *caller* owns not calling this every frame. <c>BankZone</c> fires it on
        /// the crossing into the plaza, not while standing in it.
        /// </summary>
        public BankReceipt Bank()
        {
            var units = _carried.Carried;
            var value = _carriedValue;

            BankedTotal += value;
            _carried = _carried.Cleared();
            _carriedValue = 0f;

            var receipt = new BankReceipt(units, value, BankedTotal);
            CarriedChanged?.Invoke(_carried);
            Banked?.Invoke(receipt);
            return receipt;
        }

        /// <summary>
        /// Death out in the world. **All** unbanked cargo, and nothing else - D4 read literally:
        /// its "only" limits which categories are at risk, not what fraction of them.
        ///
        /// <see cref="BankedTotal"/> is not touched here, not clamped, not recomputed. That is the
        /// one assertion in this phase that must never be softened, and the cheapest way to keep it
        /// true is for this method to contain no arithmetic on it at all.
        /// </summary>
        public float LoseCarried()
        {
            var lost = _carriedValue;

            _carried = _carried.Cleared();
            _carriedValue = 0f;

            CarriedChanged?.Invoke(_carried);
            Lost?.Invoke(lost);
            return lost;
        }

        /// <summary>Grows the hold without spilling what is in it. §4's Base upgrade, when it exists.</summary>
        public void SetCapacity(float capacity)
        {
            _carried = _carried.WithCapacity(capacity);
            CarriedChanged?.Invoke(_carried);
        }
    }

    /// <summary>What one banking was worth. Phase 5 reads this to know how big a payoff to show.</summary>
    public readonly struct BankReceipt
    {
        /// <summary>Cargo units handed over. Zero for a player who walked home empty.</summary>
        public readonly float Units;

        /// <summary>What those units were worth, given where they were gathered.</summary>
        public readonly float Value;

        /// <summary>The permanent total, after.</summary>
        public readonly float Total;

        public BankReceipt(float units, float value, float total)
        {
            Units = units;
            Value = value;
            Total = total;
        }

        public bool WasEmpty => Units <= 0f;

        public override string ToString() => $"banked {Units:0.#} units worth {Value:0.#} (total {Total:0.#})";
    }
}
