using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// A Relic Weapon is the chassis grafts attach to (GDD S7). Its class reshapes every
    /// graft ability; its tier and level are flat stat growth on top.
    /// </summary>
    public sealed class WeaponDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly WeaponClass Class;
        public readonly Rarity Rarity;
        public readonly float BasePower;
        public readonly float BaseAttackSpeed;

        public WeaponDefinition(string id, string displayName, WeaponClass cls, Rarity rarity,
            float basePower, float baseAttackSpeed)
        {
            Id = id;
            DisplayName = displayName;
            Class = cls;
            Rarity = rarity;
            BasePower = basePower;
            BaseAttackSpeed = baseAttackSpeed;
        }
    }

    public static class WeaponCatalog
    {
        public static readonly WeaponDefinition RustEdge = new WeaponDefinition(
            "rust_edge", "Rust Edge", WeaponClass.Blade, Rarity.Common, 6f, 1.0f);

        public static readonly WeaponDefinition BoneSaw = new WeaponDefinition(
            "bone_saw", "Bone Saw", WeaponClass.Blade, Rarity.Rare, 11f, 1.1f);

        public static readonly WeaponDefinition SlugThrower = new WeaponDefinition(
            "slug_thrower", "Slug Thrower", WeaponClass.Firearm, Rarity.Common, 5f, 1.15f);

        public static readonly WeaponDefinition SplitCaster = new WeaponDefinition(
            "split_caster", "Split Caster", WeaponClass.Firearm, Rarity.Epic, 13f, 1.05f);

        public static readonly IReadOnlyList<WeaponDefinition> All = new[]
        {
            RustEdge, BoneSaw, SlugThrower, SplitCaster
        };

        public static WeaponDefinition ById(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return RustEdge;
        }
    }

    /// <summary>
    /// One owned copy of a weapon, with the level bought using Weapon Cores.
    /// </summary>
    public sealed class OwnedWeapon
    {
        public string DefinitionId;
        public Rarity Tier;
        public int Level = 1;

        public WeaponDefinition Definition => WeaponCatalog.ById(DefinitionId);

        /// <summary>Tier is the merge axis, level is the currency axis -- deliberately
        /// independent, so merging never wipes invested Cores.</summary>
        public float Power
        {
            get
            {
                var def = Definition;
                float tierScale = 1f + 0.5f * ((int)Tier - (int)def.Rarity);
                if (tierScale < 1f) tierScale = 1f;
                return def.BasePower * tierScale * (1f + 0.12f * (Level - 1));
            }
        }
    }

    /// <summary>
    /// The merge rule (GDD S7): three copies at the same tier fuse into one at the next.
    /// </summary>
    public static class WeaponMerge
    {
        public const int CopiesPerMerge = 3;

        public static bool CanMerge(IReadOnlyList<OwnedWeapon> stack, string definitionId, Rarity tier)
        {
            return CountAt(stack, definitionId, tier) >= CopiesPerMerge && tier < Rarity.Legendary;
        }

        public static int CountAt(IReadOnlyList<OwnedWeapon> stack, string definitionId, Rarity tier)
        {
            int n = 0;
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i].DefinitionId == definitionId && stack[i].Tier == tier) n++;
            }
            return n;
        }

        /// <summary>Consumes three copies and returns the upgraded one. The highest level
        /// among the consumed copies is kept, so merging never costs the player Cores.</summary>
        public static OwnedWeapon Merge(List<OwnedWeapon> stack, string definitionId, Rarity tier)
        {
            if (!CanMerge(stack, definitionId, tier)) return null;

            int bestLevel = 1;
            int removed = 0;
            for (int i = stack.Count - 1; i >= 0 && removed < CopiesPerMerge; i--)
            {
                if (stack[i].DefinitionId != definitionId || stack[i].Tier != tier) continue;
                if (stack[i].Level > bestLevel) bestLevel = stack[i].Level;
                stack.RemoveAt(i);
                removed++;
            }

            var merged = new OwnedWeapon
            {
                DefinitionId = definitionId,
                Tier = (Rarity)((int)tier + 1),
                Level = bestLevel
            };
            stack.Add(merged);
            return merged;
        }
    }
}
