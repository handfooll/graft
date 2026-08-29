using System;

namespace Graft.Core
{
    /// <summary>
    /// One drop of essence, as a value. What it costs to carry, what family it came from, and
    /// where it was killed.
    ///
    /// All three matter later and only one matters now. <see cref="CargoUnits"/> is what Phase 4
    /// spends; <see cref="Origin"/> is what Phase 4 banks it for, through
    /// <see cref="EssenceValues"/>; <see cref="Family"/> is carried purely for Phase 5, which fills
    /// a meter per family and offers a graft when one tops out (DESIGN.md §5). Recording it now
    /// costs nothing and means Phase 5 does not have to reopen the drop path to find out what died.
    /// </summary>
    public readonly struct EssenceMote
    {
        public readonly EnemyFamily Family;

        /// <summary>Cargo consumed by carrying this. Every mote in the game costs the same.</summary>
        public readonly float CargoUnits;

        /// <summary>
        /// Where the enemy died - **not** where the player is standing when it is absorbed. A mote
        /// is worth what the ground it came off is worth, and a player who kills at the Ember Scar
        /// border and steps into Bone Reach to collect has not made their haul cheaper.
        /// </summary>
        public readonly Area Origin;

        public EssenceMote(EnemyFamily family, float cargoUnits, Area origin)
        {
            Family = family;
            CargoUnits = MathF.Max(0f, cargoUnits);
            Origin = origin;
        }

        public override string ToString() => $"{Family} x{CargoUnits:0.#} from {Origin}";
    }
}
