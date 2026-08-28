namespace Graft.Core
{
    /// <summary>
    /// The four kinds of thing that live out there. DESIGN.md §5 names them, and names them as an
    /// *axis of grafting*: the family decides **what an ability does**, independently of the slot
    /// it is grafted into and the weapon carrying it.
    ///
    /// So this enum is not a bestiary - it is one third of Phase 5's resolver table, arriving
    /// early because Phase 3 is what makes essence of each kind droppable. Do not add a fifth
    /// family without editing DESIGN.md: the resolver is (family x slot x weapon), and a family
    /// added here multiplies into every graft in the game.
    /// </summary>
    public enum EnemyFamily
    {
        /// <summary>Fast, fragile, arrives in numbers. Grafts to damage over time.</summary>
        Venom = 0,

        /// <summary>Slow and heavy. One hit hurts. Grafts to burst and knockback.</summary>
        Impact = 1,

        /// <summary>Armoured and patient. Hard to remove. Grafts to mitigation.</summary>
        Hide = 2,

        /// <summary>Keeps its distance and shoots. Grafts to range and chaining.</summary>
        Spark = 3,
    }

    public static class EnemyFamilies
    {
        public static readonly EnemyFamily[] All =
        {
            EnemyFamily.Venom, EnemyFamily.Impact, EnemyFamily.Hide, EnemyFamily.Spark,
        };

        public static int Count => All.Length;

        public static int IndexOf(EnemyFamily family) => (int)family;
    }
}
