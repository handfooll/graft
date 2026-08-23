using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// An enemy family. Killing these drops Essence shaped like this family, and filling
    /// that family's meter is what offers a Graft (GDD S4). New archetypes are the only
    /// source of new graft options, which is why world progression matters (GDD S8).
    /// </summary>
    public sealed class EnemyArchetype
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Element Element;
        /// <summary>Essence needed to fill this family's meter and trigger a graft offer.</summary>
        public readonly int EssenceToGraft;
        public readonly float BaseHP;
        public readonly float ContactDamage;
        public readonly float MoveSpeed;
        /// <summary>Passive stat bump the graft carries alongside its active ability (GDD S6).</summary>
        public readonly StatBlock PassiveBonus;
        public readonly string TraitBlurb;

        public EnemyArchetype(string id, string displayName, Element element, int essenceToGraft,
            float baseHP, float contactDamage, float moveSpeed, StatBlock passiveBonus, string traitBlurb)
        {
            Id = id;
            DisplayName = displayName;
            Element = element;
            EssenceToGraft = essenceToGraft;
            BaseHP = baseHP;
            ContactDamage = contactDamage;
            MoveSpeed = moveSpeed;
            PassiveBonus = passiveBonus;
            TraitBlurb = traitBlurb;
        }
    }

    public static class ArchetypeCatalog
    {
        public static readonly EnemyArchetype Venomspitter = new EnemyArchetype(
            "venomspitter", "Venomspitter", Element.Venom, 8,
            baseHP: 22f, contactDamage: 6f, moveSpeed: 1.9f,
            passiveBonus: new StatBlock { Power = 2f, CritChance = 0.02f },
            traitBlurb: "Lingering poison that ticks after the hit lands.");

        public static readonly EnemyArchetype Bloodcharger = new EnemyArchetype(
            "bloodcharger", "Bloodcharger", Element.Impact, 10,
            baseHP: 34f, contactDamage: 11f, moveSpeed: 3.1f,
            passiveBonus: new StatBlock { Power = 4f, MoveSpeed = 0.25f },
            traitBlurb: "Heavy burst damage that knocks what it touches backwards.");

        public static readonly EnemyArchetype Hidebrute = new EnemyArchetype(
            "hidebrute", "Hidebrute", Element.Hide, 6,
            baseHP: 60f, contactDamage: 9f, moveSpeed: 1.3f,
            passiveBonus: new StatBlock { MaxHP = 22f, HPRegen = 0.4f },
            traitBlurb: "Thickened hide. Mostly a wall of extra health.");

        public static readonly EnemyArchetype Sparkmite = new EnemyArchetype(
            "sparkmite", "Sparkmite", Element.Spark, 12,
            baseHP: 14f, contactDamage: 4f, moveSpeed: 4.0f,
            passiveBonus: new StatBlock { AttackSpeed = 0.18f, PickupRadius = 0.4f },
            traitBlurb: "Arcs to a second target near the first.");

        public static readonly EnemyArchetype HollowChoir = new EnemyArchetype(
            "hollowchoir", "Hollow Choir", Element.Hollow, 4,
            baseHP: 420f, contactDamage: 18f, moveSpeed: 1.6f,
            passiveBonus: new StatBlock { Power = 9f, MaxHP = 30f, CritChance = 0.06f },
            traitBlurb: "Boss essence. Everything, but it corrupts you twice as fast.");

        public static readonly IReadOnlyList<EnemyArchetype> All = new[]
        {
            Venomspitter, Bloodcharger, Hidebrute, Sparkmite, HollowChoir
        };

        /// <summary>The archetypes that spawn as regular wave enemies (the boss is separate).</summary>
        public static readonly IReadOnlyList<EnemyArchetype> WaveSpawnable = new[]
        {
            Venomspitter, Bloodcharger, Hidebrute, Sparkmite
        };

        public static EnemyArchetype ById(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }
    }
}
