namespace Graft.Core
{
    /// <summary>
    /// The six places in the world (DESIGN.md §7: Base plus five biomes, one continuous world).
    ///
    /// The order is the order you meet them walking out from Base, and it is load-bearing: the
    /// integer value IS the distance rank, which is what makes "distance from Base is the
    /// difficulty dial" something the code can assert rather than something a level designer has
    /// to remember. Phase 3's spawn tables and Phase 4's banking both key off this enum.
    /// </summary>
    public enum Area
    {
        /// <summary>Safe hub. Bank, upgrades, no enemies (§7).</summary>
        Base = 0,

        /// <summary>Tutorial-tier. Nearest Base, weakest enemies, safest walk home.</summary>
        Verge = 1,

        RustFlats = 2,
        GlassMire = 3,
        BoneReach = 4,

        /// <summary>Furthest out. Best loot, longest and most dangerous return.</summary>
        EmberScar = 5,
    }

    public static class Areas
    {
        /// <summary>Void - not part of the world at all. Used as a tile value, never as an Area.</summary>
        public const int None = -1;

        public static readonly Area[] All =
        {
            Area.Base, Area.Verge, Area.RustFlats, Area.GlassMire, Area.BoneReach, Area.EmberScar,
        };

        public static readonly Area[] Biomes =
        {
            Area.Verge, Area.RustFlats, Area.GlassMire, Area.BoneReach, Area.EmberScar,
        };

        public static bool IsBiome(Area area) => area != Area.Base;

        /// <summary>
        /// How far out this area sits, 0 for Base. Identical to the enum value by design - see the
        /// note on <see cref="Area"/> - but named, so the reason a comparison is legal is visible
        /// at the call site instead of looking like enum arithmetic.
        /// </summary>
        public static int DistanceRank(Area area) => (int)area;
    }
}
