using System;

namespace Graft.Core
{
    /// <summary>
    /// The eight stats the GDD's Vessel Mastery tree spends on (S6), plus the derived
    /// values combat reads. Additive fields are summed, multiplicative ones multiply,
    /// so a graft's "+12% Max HP" and a mastery node's "+40 flat HP" compose cleanly.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        public float Power;
        public float MaxHP;
        public float HPRegen;
        public float AttackSpeed;
        public float CritChance;
        public float MoveSpeed;
        public float PickupRadius;
        public float CorruptionResist;

        public static StatBlock Zero => default;

        /// <summary>The baseline every Vessel starts from before mastery or grafts.</summary>
        public static StatBlock Baseline => new StatBlock
        {
            Power = 10f,
            MaxHP = 100f,
            HPRegen = 0.5f,
            AttackSpeed = 1f,
            CritChance = 0.05f,
            MoveSpeed = 4.2f,
            PickupRadius = 1.6f,
            CorruptionResist = 0f
        };

        public static StatBlock operator +(StatBlock a, StatBlock b) => new StatBlock
        {
            Power = a.Power + b.Power,
            MaxHP = a.MaxHP + b.MaxHP,
            HPRegen = a.HPRegen + b.HPRegen,
            AttackSpeed = a.AttackSpeed + b.AttackSpeed,
            CritChance = a.CritChance + b.CritChance,
            MoveSpeed = a.MoveSpeed + b.MoveSpeed,
            PickupRadius = a.PickupRadius + b.PickupRadius,
            CorruptionResist = a.CorruptionResist + b.CorruptionResist
        };

        /// <summary>Component-wise scale by a multiplier block where 1 means "unchanged".</summary>
        public StatBlock Scaled(StatBlock mult) => new StatBlock
        {
            Power = Power * mult.Power,
            MaxHP = MaxHP * mult.MaxHP,
            HPRegen = HPRegen * mult.HPRegen,
            AttackSpeed = AttackSpeed * mult.AttackSpeed,
            CritChance = CritChance * mult.CritChance,
            MoveSpeed = MoveSpeed * mult.MoveSpeed,
            PickupRadius = PickupRadius * mult.PickupRadius,
            CorruptionResist = CorruptionResist * mult.CorruptionResist
        };

        public static StatBlock One => new StatBlock
        {
            Power = 1f, MaxHP = 1f, HPRegen = 1f, AttackSpeed = 1f,
            CritChance = 1f, MoveSpeed = 1f, PickupRadius = 1f, CorruptionResist = 1f
        };
    }
}
