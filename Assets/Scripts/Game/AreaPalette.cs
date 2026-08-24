using System;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// What one area looks like. DESIGN.md §7: "biome identity must be readable in a single
    /// screenshot", and BUILD_PLAN.md's Phase 2 gate is exactly that test - six pictures, each
    /// identifiable as a different place with nothing else to go on.
    ///
    /// Two colours for the ground rather than one: a single flat fill under a top-down camera has
    /// no scale and no texture, so a screenshot of it is a screenshot of nothing. Scattering a
    /// second shade across the tiles gives the floor grain without needing art.
    ///
    /// Colours only - the *shapes* scattered in each area are chosen by the editor's prop kit from
    /// the area itself, because a silhouette is not a value you can tune in an inspector.
    /// </summary>
    [Serializable]
    public class AreaPalette
    {
        public Area area;

        [Tooltip("Shown to the player, and used to name the generated ground objects and meshes.")]
        public string displayName = "";

        [Tooltip("The dominant ground colour. This is what the gate screenshot is judged on first.")]
        public Color ground = Color.grey;

        [Tooltip("Scattered over roughly a fifth of the tiles, to give the floor grain. Keep it " +
                 "within about a tenth of the ground colour: a tile is 4 m and the camera sees " +
                 "9.5 m, so anything more contrasty stops being grain and becomes a checkerboard.")]
        public Color groundMottle = Color.grey;

        [Tooltip("The body colour of this area's scatter props.")]
        public Color prop = Color.grey;

        [Tooltip("The second colour on a prop - a tip, a core, a vent. Carries the area's signature.")]
        public Color propAccent = Color.white;

        [Tooltip("The deepest colour in the area, for the things that should recede rather than " +
                 "stand out: still water, cracked plates, scattered rubble. Darker than the ground, " +
                 "always - a flat decoration lighter than the floor reads as a hole cut in it.")]
        public Color propShade = Color.black;

        [Tooltip("Whether the accent colour glows. Only Ember Scar should; a lit world with one " +
                 "emissive area makes that area unmistakable at a glance.")]
        public bool accentEmissive;

        [Tooltip("Props per 100 square metres. The camera shows roughly 250 square metres, so this " +
                 "is very nearly 'props visible in a screenshot, times four'.")]
        [Range(0f, 6f)]
        public float propsPer100SqMetres = 1.4f;

        [Tooltip("Metres between prop centres. Below about half the prop's own size they interpenetrate.")]
        [Range(1f, 20f)]
        public float propSpacing = 5f;
    }
}
