namespace Graft.Core
{
    /// <summary>
    /// One graft actually installed in a slot for the current run.
    /// </summary>
    public sealed class GraftInstance
    {
        public readonly EnemyArchetype Source;
        public readonly GraftSlot Slot;
        /// <summary>Reinforced when the same family is grafted into the same slot again.</summary>
        public int Tier;

        public GraftInstance(EnemyArchetype source, GraftSlot slot, int tier = 1)
        {
            Source = source;
            Slot = slot;
            Tier = tier;
        }
    }

    /// <summary>
    /// One option on the level-up card: "this family, in this slot". The player picks the
    /// slot, which is the decision the GDD is built around.
    /// </summary>
    public struct GraftOffer
    {
        public EnemyArchetype Source;
        public GraftSlot Slot;
        public AbilitySpec Resolved;
        /// <summary>What this choice would replace, if anything. Null when the slot is empty.</summary>
        public GraftInstance Replaces;
        /// <summary>True when it reinforces the same family already in that slot.</summary>
        public bool IsReinforcement;
        public float CorruptionCost;
    }
}
