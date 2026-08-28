namespace Graft.Core
{
    /// <summary>
    /// Numbers that DESIGN.md locks down, in one place so nothing hardcodes them twice.
    /// Anything here is a decision, not a tuning value - tuning lives in ScriptableObjects.
    /// </summary>
    public static class DesignContract
    {
        /// <summary>Base plus five biomes, all in one continuous world (D3, §7).</summary>
        public const int BiomeCount = 5;

        /// <summary>Base is walkable-continuous with the biomes, so it counts as an area too.</summary>
        public const int AreaCount = BiomeCount + 1;

        /// <summary>Head, Core, LeftArm, RightArm, Legs (§5).</summary>
        public const int BodySlotCount = 5;

        /// <summary>venom / impact / hide / spark (§5) - the "what the ability does" axis.</summary>
        public const int EnemyFamilyCount = 4;

        /// <summary>Base is the safe hub: no enemies ever spawn there (§7). Enforced in SpawnDirector.</summary>
        public const bool BaseIsSafe = true;

        /// <summary>Corruption fraction past which the player burns their own health (§6).</summary>
        public const float SelfDamageThreshold = 0.85f;

        /// <summary>Corruption resets on banking at Base, never on a timer (D5).</summary>
        public const bool CorruptionResetsOnBankOnly = true;
    }
}
