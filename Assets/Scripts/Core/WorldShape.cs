namespace Graft.Core
{
    /// <summary>
    /// How the world is generated. Two rules, both real: the project keeps a layout asset for each
    /// so they can be swapped and compared on the phone rather than argued about on paper.
    /// </summary>
    public enum WorldShape
    {
        /// <summary>
        /// Biomes as transverse bands along one curving spine. Maximum difficulty gradient - the
        /// walk home from the deepest biome crosses every other one - but the world is
        /// one-dimensional, so getting from any biome to any other means traversing everything
        /// between them.
        /// </summary>
        Corridor = 0,

        /// <summary>
        /// Biomes as overlapping discs packed around Base. Every biome borders two to four others,
        /// so travel between them is a hop rather than a traverse, and the gradient survives
        /// because the discs sit at ascending distances from Base.
        /// </summary>
        Cluster = 1,
    }
}
