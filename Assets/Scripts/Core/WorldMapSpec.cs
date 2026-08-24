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
        /// <summary>The spine, from Base outward. See <see cref="WorldPath"/>.</summary>
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

        /// <summary>Half-width of the walkable corridor at Base, and at the far end.</summary>
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
        /// </summary>
        public Lobe[] Lobes = Array.Empty<Lobe>();

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
    }
}
