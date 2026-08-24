using System.Collections.Generic;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The authored world: where the spine runs, how wide the world is along it, where each biome
    /// ends, and what each area looks like. A ScriptableObject because the shape of the world is
    /// tuning (BUILD_PLAN.md rule 3) - the editor's world builder and the runtime's area lookup
    /// both read this one asset, so there is never a second copy of the layout to drift.
    ///
    /// The defaults below are the layout itself, not placeholders: they have to be, or a fresh
    /// clone would configure itself an empty world.
    ///
    /// **Distances are in seconds of walking**, per STATUS.md. At 5.5 m/s the spine is a little
    /// over a minute end to end, unopposed - which is what the walk home from Ember Scar costs
    /// before anything tries to stop you.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldLayout", menuName = "GRAFT/World Layout")]
    public class WorldLayout : ScriptableObject
    {
        [Header("Spine")]
        [Tooltip("The path from Base outward, in metres. Biomes are bands along it, so this is " +
                 "also the difficulty gradient (DESIGN.md §7).")]
        public Vector2[] pathPoints =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 70f),
            new Vector2(28f, 138f),
            new Vector2(10f, 205f),
            new Vector2(-40f, 250f),
            new Vector2(-30f, 320f),
        };

        [Tooltip("Where each biome ends, as a fraction of the spine. Ascending; the last is 1.")]
        public float[] biomeEndFractions = { 0.24f, 0.44f, 0.62f, 0.81f, 1f };

        [Header("Extent")]
        [Tooltip("Edge of one ground tile, in metres. Also the resolution of every biome border.")]
        [Range(1f, 12f)]
        public float tileSize = 4f;

        [Tooltip("Radius of the safe plaza. Everything inside it is Base.")]
        [Range(8f, 60f)]
        public float baseRadius = 20f;

        [Tooltip("Half-width of the walkable world at Base, and at the far end. It widens as it " +
                 "goes out, so the deep biomes are places to get lost in rather than corridors.")]
        public float halfWidthNear = 46f;
        public float halfWidthFar = 74f;

        [Tooltip("How far the walkable edge wanders, and over what wavelength. Without it the " +
                 "world is a ruler-straight tube.")]
        public float edgeWaveAmplitude = 11f;
        public float edgeWaveLength = 85f;

        [Tooltip("The same, for the borders between biomes.")]
        public float borderWaveAmplitude = 13f;
        public float borderWaveLength = 120f;

        [Tooltip("Bulges hanging off the spine, so the world has pockets and dead ends. Each must " +
                 "overlap the corridor or it is an unreachable island - WorldMapTests proves it.")]
        public Lobe[] lobes =
        {
            new Lobe { centre = new Vector2(-58f, 40f), radius = 26f },
            new Lobe { centre = new Vector2(72f, 130f), radius = 30f },
            new Lobe { centre = new Vector2(58f, 200f), radius = 28f },
            new Lobe { centre = new Vector2(-95f, 232f), radius = 30f },
            new Lobe { centre = new Vector2(32f, 330f), radius = 32f },
        };

        [Header("Boundary")]
        [Tooltip("Wall height. Tall enough that the camera cannot see over it from inside - at " +
                 "this pitch a shorter wall shows a strip of dead ground above its top edge.")]
        [Range(2f, 12f)]
        public float boundaryHeight = 5.5f;

        [Range(0.2f, 3f)]
        public float boundaryThickness = 0.9f;

        public Color boundaryColour = new Color(0.195f, 0.188f, 0.205f);

        [Tooltip("Dead ground generated outside the wall. It exists only so the frame never " +
                 "contains the camera's clear colour, which reads as a rendering fault.")]
        public Color voidColour = new Color(0.055f, 0.055f, 0.065f);

        [Range(0, 20)]
        public int voidSkirtTiles = 6;

        public int seed = 20260824;

        [Header("Areas")]
        public AreaPalette[] palettes = DefaultPalettes();

        public WorldMapSpec ToSpec()
        {
            var points = new Vec2[pathPoints.Length];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vec2(pathPoints[i].x, pathPoints[i].y);
            }

            var discs = new WorldMapSpec.Lobe[lobes.Length];
            for (var i = 0; i < discs.Length; i++)
            {
                discs[i] = new WorldMapSpec.Lobe(new Vec2(lobes[i].centre.x, lobes[i].centre.y), lobes[i].radius);
            }

            return new WorldMapSpec
            {
                PathPoints = points,
                TileSize = tileSize,
                BaseRadius = baseRadius,
                HalfWidthNear = halfWidthNear,
                HalfWidthFar = halfWidthFar,
                EdgeWaveAmplitude = edgeWaveAmplitude,
                EdgeWaveLength = edgeWaveLength,
                BorderWaveAmplitude = borderWaveAmplitude,
                BorderWaveLength = borderWaveLength,
                BiomeEndFractions = (float[])biomeEndFractions.Clone(),
                Lobes = discs,
                VoidSkirtTiles = voidSkirtTiles,
                Seed = seed,
            };
        }

        public WorldMap BuildMap() => new WorldMap(ToSpec());

        public AreaPalette PaletteFor(Area area)
        {
            foreach (var palette in palettes)
            {
                if (palette.area == area)
                {
                    return palette;
                }
            }

            throw new KeyNotFoundException($"WorldLayout has no palette for {area}.");
        }

        /// <summary>
        /// Six areas that do not look like one another. The test the gate applies is whether a
        /// stranger shown two of these pictures could say they were different places, so the
        /// colours are pulled apart on hue as well as value: slate, moss, rust, teal, bone, ash.
        /// </summary>
        static AreaPalette[] DefaultPalettes() => new[]
        {
            new AreaPalette
            {
                area = Area.Base, displayName = "The Anchor",
                ground = new Color(0.360f, 0.390f, 0.440f),
                groundMottle = new Color(0.336f, 0.364f, 0.412f),
                prop = new Color(0.620f, 0.650f, 0.700f),
                propAccent = new Color(0.300f, 0.780f, 0.850f),
                propShade = new Color(0.270f, 0.295f, 0.335f),
                accentEmissive = true,
                // Base is built, not grown: its contents are placed by hand, not scattered.
                propsPer100SqMetres = 0f, propSpacing = 6f,
            },
            new AreaPalette
            {
                area = Area.Verge, displayName = "The Verge",
                ground = new Color(0.235f, 0.295f, 0.205f),
                groundMottle = new Color(0.218f, 0.276f, 0.190f),
                prop = new Color(0.420f, 0.430f, 0.360f),
                propAccent = new Color(0.550f, 0.600f, 0.320f),
                propShade = new Color(0.160f, 0.200f, 0.140f),
                propsPer100SqMetres = 1.5f, propSpacing = 5f,
            },
            new AreaPalette
            {
                area = Area.RustFlats, displayName = "Rust Flats",
                ground = new Color(0.355f, 0.205f, 0.130f),
                groundMottle = new Color(0.330f, 0.187f, 0.116f),
                prop = new Color(0.520f, 0.270f, 0.140f),
                propAccent = new Color(0.870f, 0.470f, 0.170f),
                propShade = new Color(0.235f, 0.125f, 0.080f),
                propsPer100SqMetres = 1.6f, propSpacing = 5f,
            },
            new AreaPalette
            {
                area = Area.GlassMire, displayName = "Glass Mire",
                ground = new Color(0.125f, 0.225f, 0.245f),
                groundMottle = new Color(0.110f, 0.206f, 0.225f),
                prop = new Color(0.350f, 0.620f, 0.650f),
                propAccent = new Color(0.720f, 0.930f, 0.950f),
                propShade = new Color(0.042f, 0.082f, 0.098f),
                propsPer100SqMetres = 1.7f, propSpacing = 4.5f,
            },
            new AreaPalette
            {
                area = Area.BoneReach, displayName = "Bone Reach",
                ground = new Color(0.545f, 0.505f, 0.410f),
                groundMottle = new Color(0.514f, 0.476f, 0.386f),
                prop = new Color(0.885f, 0.865f, 0.795f),
                propAccent = new Color(0.640f, 0.600f, 0.515f),
                propShade = new Color(0.400f, 0.365f, 0.290f),
                propsPer100SqMetres = 1.1f, propSpacing = 7f,
            },
            new AreaPalette
            {
                area = Area.EmberScar, displayName = "Ember Scar",
                ground = new Color(0.145f, 0.128f, 0.130f),
                groundMottle = new Color(0.128f, 0.113f, 0.116f),
                prop = new Color(0.245f, 0.205f, 0.200f),
                propAccent = new Color(0.960f, 0.235f, 0.055f),
                propShade = new Color(0.068f, 0.058f, 0.058f),
                accentEmissive = true,
                // Denser than the rest. Near-black ground with sparse black rock photographs as
                // an underexposed frame rather than as a place; what makes this area read is how
                // much of it is glowing.
                propsPer100SqMetres = 2.2f, propSpacing = 4.2f,
            },
        };

        [System.Serializable]
        public struct Lobe
        {
            public Vector2 centre;
            public float radius;
        }
    }
}
