using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// One place for every colour in the game, so the whole thing reads as one product.
    /// Elements are colour-coded consistently: an enemy, the essence it drops, its meter,
    /// and the graft ability it becomes are all the same hue, which is how the player
    /// learns "that thing gives me poison" without being told.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Floor = new Color(0.13f, 0.12f, 0.16f);
        public static readonly Color FloorGrid = new Color(0.18f, 0.17f, 0.23f);
        public static readonly Color Wall = new Color(0.30f, 0.16f, 0.20f);

        public static readonly Color HeroClean = new Color(0.85f, 0.86f, 0.90f);
        public static readonly Color HeroMonstrous = new Color(0.55f, 0.13f, 0.20f);

        public static readonly Color Health = new Color(0.83f, 0.24f, 0.31f);
        public static readonly Color HealthBack = new Color(0.20f, 0.10f, 0.13f);

        public static readonly Color PurityClean = new Color(0.62f, 0.86f, 0.94f);
        public static readonly Color PurityTainted = new Color(0.85f, 0.63f, 0.30f);
        public static readonly Color PurityMonstrous = new Color(0.78f, 0.19f, 0.36f);

        public static readonly Color Panel = new Color(0.10f, 0.09f, 0.13f, 0.94f);
        public static readonly Color PanelEdge = new Color(0.35f, 0.30f, 0.42f);
        public static readonly Color Text = new Color(0.92f, 0.91f, 0.95f);
        public static readonly Color TextDim = new Color(0.62f, 0.60f, 0.70f);
        public static readonly Color Gold = new Color(0.95f, 0.80f, 0.35f);

        public static Color For(Element element)
        {
            switch (element)
            {
                case Element.Venom: return new Color(0.51f, 0.84f, 0.33f);
                case Element.Impact: return new Color(0.93f, 0.44f, 0.26f);
                case Element.Hide: return new Color(0.72f, 0.60f, 0.44f);
                case Element.Spark: return new Color(0.44f, 0.76f, 0.98f);
                default: return new Color(0.74f, 0.42f, 0.92f);
            }
        }

        public static Color For(PurityBand band)
        {
            switch (band)
            {
                case PurityBand.Focused: return PurityClean;
                case PurityBand.Tainted: return PurityTainted;
                default: return PurityMonstrous;
            }
        }

        public static Color For(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return new Color(0.70f, 0.70f, 0.75f);
                case Rarity.Uncommon: return new Color(0.48f, 0.82f, 0.45f);
                case Rarity.Rare: return new Color(0.40f, 0.66f, 0.95f);
                case Rarity.Epic: return new Color(0.73f, 0.45f, 0.94f);
                default: return new Color(0.97f, 0.72f, 0.28f);
            }
        }

        public static Color Hero(float corruption) => Color.Lerp(HeroClean, HeroMonstrous, corruption);
    }
}
