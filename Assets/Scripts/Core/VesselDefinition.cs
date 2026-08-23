using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// A Vessel is a *body*, never a kit (GDD S5). It has a silhouette, one passive
    /// identity trait, and a stat baseline -- but no built-in abilities, because every
    /// ability in the game still comes from grafting. That is what keeps a collectible
    /// hero layer from contradicting the absorption fantasy.
    /// </summary>
    public sealed class VesselDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Rarity Rarity;
        public readonly StatBlock Baseline;
        /// <summary>The element this body accepts more readily. Null-safe: Hollow means none.</summary>
        public readonly Element? Affinity;
        /// <summary>Extra damage multiplier applied to grafts matching <see cref="Affinity"/>.</summary>
        public readonly float AffinityBonus;
        /// <summary>0..1 reduction to corruption gained per graft.</summary>
        public readonly float CorruptionResistance;
        public readonly string IdentityBlurb;
        /// <summary>Base silhouette width/height ratio, so bodies read differently on screen.</summary>
        public readonly float SilhouetteWidth;
        public readonly float SilhouetteHeight;

        public VesselDefinition(string id, string displayName, Rarity rarity, StatBlock baseline,
            Element? affinity, float affinityBonus, float corruptionResistance, string identityBlurb,
            float silhouetteWidth, float silhouetteHeight)
        {
            Id = id;
            DisplayName = displayName;
            Rarity = rarity;
            Baseline = baseline;
            Affinity = affinity;
            AffinityBonus = affinityBonus;
            CorruptionResistance = corruptionResistance;
            IdentityBlurb = identityBlurb;
            SilhouetteWidth = silhouetteWidth;
            SilhouetteHeight = silhouetteHeight;
        }

        /// <summary>The GDD's reason to own more than one Vessel: the same drop is worth
        /// more on the body that is built for it.</summary>
        public float DamageMultiplierFor(Element element)
        {
            return Affinity.HasValue && Affinity.Value == element ? 1f + AffinityBonus : 1f;
        }
    }

    public static class VesselCatalog
    {
        public static readonly VesselDefinition Scaleborn = new VesselDefinition(
            "scaleborn", "Scaleborn", Rarity.Common,
            StatBlock.Baseline,
            affinity: Element.Venom, affinityBonus: 0.35f, corruptionResistance: 0.05f,
            identityBlurb: "Reptilian stock. Venom grafts take hold faster and hit harder.",
            silhouetteWidth: 0.62f, silhouetteHeight: 0.95f);

        public static readonly VesselDefinition Ironhusk = new VesselDefinition(
            "ironhusk", "Ironhusk", Rarity.Rare,
            new StatBlock
            {
                Power = 9f, MaxHP = 135f, HPRegen = 0.35f, AttackSpeed = 0.9f,
                CritChance = 0.03f, MoveSpeed = 3.7f, PickupRadius = 1.5f, CorruptionResist = 0f
            },
            affinity: Element.Hide, affinityBonus: 0.25f, corruptionResistance: 0.35f,
            identityBlurb: "A construct. Stays human-shaped far longer before it turns.",
            silhouetteWidth: 0.82f, silhouetteHeight: 0.88f);

        public static readonly VesselDefinition Emberkin = new VesselDefinition(
            "emberkin", "Emberkin", Rarity.Epic,
            new StatBlock
            {
                Power = 12f, MaxHP = 82f, HPRegen = 0.6f, AttackSpeed = 1.25f,
                CritChance = 0.09f, MoveSpeed = 4.8f, PickupRadius = 1.9f, CorruptionResist = 0f
            },
            affinity: Element.Spark, affinityBonus: 0.4f, corruptionResistance: -0.15f,
            identityBlurb: "Fast and thin-skinned. Corrupts quickly -- and does not mind.",
            silhouetteWidth: 0.55f, silhouetteHeight: 1.05f);

        public static readonly IReadOnlyList<VesselDefinition> All = new[]
        {
            Scaleborn, Ironhusk, Emberkin
        };

        public static VesselDefinition ById(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return Scaleborn;
        }
    }
}
