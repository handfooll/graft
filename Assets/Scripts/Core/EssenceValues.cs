using System;

namespace Graft.Core
{
    /// <summary>
    /// What a unit of cargo is worth once it is banked, by the area it was killed in.
    ///
    /// **Derived from <see cref="WorldMap.DistanceFromBase"/>, never from a table of literals.**
    /// DESIGN.md §7 makes distance from Base the difficulty dial, and the payout has to be the same
    /// dial or the deepest biome is a harder fight for the same money. Deriving it also means
    /// re-tuning the world's depth re-tunes the payout with it, instead of silently leaving a
    /// constant behind that nobody remembers to change - the same trick, and the same reason, as
    /// the Phase 2 depth test.
    ///
    /// Normalised so the nearest biome is exactly 1.0, which makes "a full bar from Ember Scar
    /// banks 4.1x a full bar from the Verge" a *ratio* of two things this class computes rather
    /// than a number written down anywhere.
    /// </summary>
    public sealed class EssenceValues
    {
        readonly float[] _perUnit;

        public EssenceValues(WorldMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            _perUnit = new float[Areas.All.Length];

            var nearest = float.MaxValue;
            foreach (var area in Areas.Biomes)
            {
                nearest = MathF.Min(nearest, DistanceTo(map, area));
            }

            // A degenerate layout with Base and the nearest biome on the same spot would divide by
            // zero and make every biome infinitely valuable. WorldMapTests already refuses such a
            // layout; this refuses to produce nonsense out of one that got through anyway.
            nearest = MathF.Max(1f, nearest);

            foreach (var area in Areas.All)
            {
                // Base floors at the nearest biome's value rather than at zero. Nothing spawns in
                // Base (§7) so a mote should never originate there - but the origin is read off a
                // tile lookup at the moment of death, and an enemy killed on the Base border can
                // fall inside the plaza. Worth 1.0 is a rounding error; worth 0.0 would be a haul
                // that silently evaporated, and the player would have no way to know why.
                _perUnit[(int)area] = MathF.Max(1f, DistanceTo(map, area) / nearest);
            }
        }

        static float DistanceTo(WorldMap map, Area area) => map.DistanceFromBase(map.AnchorFor(area));

        /// <summary>Banked value of one unit of cargo gathered in <paramref name="area"/>.</summary>
        public float PerUnit(Area area)
        {
            var rank = Areas.DistanceRank(area);
            return rank >= 0 && rank < _perUnit.Length ? _perUnit[rank] : 1f;
        }

        public float ValueOf(EssenceMote mote) => mote.CargoUnits * PerUnit(mote.Origin);

        /// <summary>
        /// What a completely full bar of <paramref name="area"/>'s essence banks for. The number
        /// the extraction decision is actually made on, and the one the depth-ratio test compares.
        /// </summary>
        public float FullBarValue(Area area, float capacity) => capacity * PerUnit(area);

        /// <summary>
        /// How much better the deepest biome pays than the shallowest, per unit carried. Around
        /// 4.1 for the shipped cluster - but computed, so re-tuning depth moves it rather than
        /// making a test wrong.
        /// </summary>
        public float DepthRatio =>
            PerUnit(Areas.Biomes[Areas.Biomes.Length - 1]) / PerUnit(Areas.Biomes[0]);
    }
}
