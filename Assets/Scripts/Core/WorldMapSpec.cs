using System;

namespace Graft.Core
{
    /// <summary>
    /// Everything needed to generate the world, as plain data. The Unity-side
    /// <c>WorldLayout</c> asset is the authored copy; this is what the generator consumes, so the
    /// generation rules stay headlessly testable (BUILD_PLAN.md rule 4).
    /// </summary>
    public sealed class WorldMapSpec
    {
        /// <summary>Which generation rule to run. Everything below is used by one shape or the other.</summary>
        public WorldShape Shape = WorldShape.Corridor;

        // ------------------------------------------------------------------ corridor

        /// <summary>The spine, from Base outward. See <see cref="WorldPath"/>. Corridor only.</summary>
        public Vec2[] PathPoints = Array.Empty<Vec2>();

        /// <summary>
        /// Edge length of one ground tile, in metres. The world is built from these (BUILD_PLAN.md
        /// Phase 2: "ground built from modular tiles"), and it is also the resolution of every
        /// biome border - too coarse and the borders read as staircases under a camera that only
        /// sees 9.5 m across.
        /// </summary>
        public float TileSize = 4f;

        /// <summary>Radius of the safe plaza at the start of the path.</summary>
        public float BaseRadius = 20f;

        /// <summary>
        /// Radius of the inner **bank plaza**, at the monument. Crossing into this is what banks
        /// (Phase 4, D-P4.2); the rest of Base is safe but does nothing.
        ///
        /// Two rings rather than one because the cluster world makes the one-ring version actively
        /// harmful: Base borders several biomes, so a route between two of them can cross the safe
        /// disc, and an accidental bank ends an expedition the player was in the middle of. The
        /// alternative - a button - spends a thumb D8 does not have spare and turns the payoff into
        /// paperwork. Two rings gives a retreat valve that keeps your build *and* a bank that costs
        /// no input.
        /// </summary>
        public float BankRadius = 7f;

        /// <summary>Half-width of the walkable corridor at Base, and at the far end. Corridor only.</summary>
        public float HalfWidthNear = 46f;
        public float HalfWidthFar = 74f;

        /// <summary>
        /// How far the walkable edge wanders in and out, and over what wavelength. Without this the
        /// world is a ruler-straight tube and every boundary reads as a level-editor artefact.
        /// </summary>
        public float EdgeWaveAmplitude = 11f;
        public float EdgeWaveLength = 85f;

        /// <summary>Same idea, applied to the borders BETWEEN biomes so they are not clean cuts.</summary>
        public float BorderWaveAmplitude = 13f;
        public float BorderWaveLength = 120f;

        /// <summary>
        /// Where each biome ends, as a fraction of the path length. Five entries, ascending, the
        /// last of which is 1. Fractions rather than metres so re-routing the path does not
        /// silently strand a biome off the end of it.
        /// </summary>
        public float[] BiomeEndFractions = { 0.24f, 0.44f, 0.62f, 0.81f, 1f };

        /// <summary>
        /// Bulges hanging off the corridor. They belong to whichever biome their centre falls in,
        /// and they exist so the world has pockets and dead ends rather than being a tube of
        /// constant width. Each one must overlap the corridor; <c>WorldMapTests</c> proves it.
        /// Corridor only.
        /// </summary>
        public Lobe[] Lobes = Array.Empty<Lobe>();

        // ------------------------------------------------------------------ cluster

        /// <summary>
        /// One disc per area, indexed by <see cref="Areas.DistanceRank"/> - so entry 0 is Base and
        /// entry 5 is the deepest biome. Cluster only.
        ///
        /// The discs overlap on purpose: where two overlap they share a border, and that border is
        /// how you get from one biome to the next without going back through Base. Which biome
        /// wins an overlapped point is decided by how far *inside* each disc it is, so the border
        /// is a curve between the two centres rather than a chord.
        /// </summary>
        public AreaDisc[] AreaDiscs = Array.Empty<AreaDisc>();

        /// <summary>
        /// How much each disc's radius wanders with bearing, as a fraction of the radius. This is
        /// what stops the world being a diagram of circles - it wobbles the outer boundary and the
        /// borders between biomes at the same time, because they are the same function.
        ///
        /// Keep well under the smallest overlap margin. Too much and two discs that were touching
        /// come apart at their one contact point, which turns a biome into an unreachable island;
        /// <c>WorldMapTests</c> catches that, but it is cheaper not to cause it.
        /// </summary>
        public float DiscWobble = 0.08f;

        // ------------------------------------------------------------------ both shapes

        /// <summary>
        /// Metres of per-tile jitter applied to **which biome wins** near a border, so the two
        /// interlock instead of meeting along a staircase of whole tiles.
        ///
        /// Deliberately not applied to whether a tile is walkable at all. Jittering that as well
        /// would fray the outer boundary into loose tiles, and a tile that comes adrift from the
        /// world is an island - which the connectivity test would catch, but only after the fact.
        /// </summary>
        public float BorderDither = 2.6f;

        /// <summary>How many tiles of dead ground to generate outside the boundary.</summary>
        public int VoidSkirtTiles = 6;

        public int Seed = 20260824;

        public readonly struct Lobe
        {
            public readonly Vec2 Centre;
            public readonly float Radius;

            public Lobe(Vec2 centre, float radius)
            {
                Centre = centre;
                Radius = radius;
            }
        }

        public readonly struct AreaDisc
        {
            public readonly Vec2 Centre;
            public readonly float Radius;

            public AreaDisc(Vec2 centre, float radius)
            {
                Centre = centre;
                Radius = radius;
            }
        }
    }
}
