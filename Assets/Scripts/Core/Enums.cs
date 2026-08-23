namespace Graft.Core
{
    /// <summary>
    /// The five body slots a graft can occupy (GDD S4). The slot decides *how* an
    /// absorbed trait is delivered, never *what* the trait is.
    /// </summary>
    public enum GraftSlot
    {
        Head = 0,
        Core = 1,
        LeftArm = 2,
        RightArm = 3,
        Legs = 4
    }

    /// <summary>How a graft's ability reaches the world. Derived from the slot.</summary>
    public enum Delivery
    {
        /// <summary>Head: aimed ranged attack, benefits from the Focused precision bonus.</summary>
        Aimed = 0,
        /// <summary>Core: passive aura that pulses on a timer, no player input.</summary>
        Aura = 1,
        /// <summary>Arms: a melee arc swung in the facing direction.</summary>
        Swipe = 2,
        /// <summary>Legs: fires as a burst along the dash path.</summary>
        Dash = 3
    }

    /// <summary>
    /// The trait carried by an enemy archetype. Decides *what* the ability does.
    /// </summary>
    public enum Element
    {
        Venom = 0,
        Impact = 1,
        Hide = 2,
        Spark = 3,
        Hollow = 4
    }

    /// <summary>The chassis a graft attaches to (GDD S7). Reshapes delivery.</summary>
    public enum WeaponClass
    {
        Blade = 0,
        Firearm = 1
    }

    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>Purity bands (GDD S4). Crossing a band changes how the hero plays and looks.</summary>
    public enum PurityBand
    {
        Focused = 0,
        Tainted = 1,
        Monstrous = 2
    }
}
