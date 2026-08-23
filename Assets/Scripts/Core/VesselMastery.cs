using System;
using System.Collections.Generic;

namespace Graft.Core
{
    public enum MasteryNode
    {
        Power = 0,
        MaxHP = 1,
        HPRegen = 2,
        AttackSpeed = 3,
        CritChance = 4,
        MoveSpeed = 5,
        PickupRadius = 6,
        CorruptionResist = 7
    }

    /// <summary>
    /// The permanent between-runs layer (GDD S6). Deliberately kept as flat "number go up"
    /// because that is a large part of why the genre is replayable -- grafting replaces the
    /// in-run levelling, not this.
    /// </summary>
    [Serializable]
    public sealed class VesselMastery
    {
        public const int MaxLevel = 20;

        public string VesselId;
        public int[] Levels = new int[8];

        public VesselMastery() { }

        public VesselMastery(string vesselId)
        {
            VesselId = vesselId;
        }

        public int LevelOf(MasteryNode node)
        {
            EnsureSize();
            return Levels[(int)node];
        }

        public bool IsMaxed(MasteryNode node) => LevelOf(node) >= MaxLevel;

        /// <summary>Escalating so late nodes are a real sink rather than an instant buy.</summary>
        public int CostToUpgrade(MasteryNode node)
        {
            int lvl = LevelOf(node);
            if (lvl >= MaxLevel) return int.MaxValue;
            return 60 + 45 * lvl + 8 * lvl * lvl;
        }

        public bool TryUpgrade(MasteryNode node, ref int softGold)
        {
            if (IsMaxed(node)) return false;
            int cost = CostToUpgrade(node);
            if (softGold < cost) return false;
            softGold -= cost;
            EnsureSize();
            Levels[(int)node]++;
            return true;
        }

        /// <summary>Additive bonuses this tree contributes on top of the Vessel baseline.</summary>
        public StatBlock ToStatBlock()
        {
            EnsureSize();
            return new StatBlock
            {
                Power = 1.8f * LevelOf(MasteryNode.Power),
                MaxHP = 9f * LevelOf(MasteryNode.MaxHP),
                HPRegen = 0.15f * LevelOf(MasteryNode.HPRegen),
                AttackSpeed = 0.035f * LevelOf(MasteryNode.AttackSpeed),
                CritChance = 0.012f * LevelOf(MasteryNode.CritChance),
                MoveSpeed = 0.07f * LevelOf(MasteryNode.MoveSpeed),
                PickupRadius = 0.12f * LevelOf(MasteryNode.PickupRadius),
                // Corruption Resist is the node the GDD calls out as new to this design:
                // it buys you more grafts before you go fully monstrous.
                CorruptionResist = 0.025f * LevelOf(MasteryNode.CorruptionResist)
            };
        }

        public static string DisplayName(MasteryNode node)
        {
            switch (node)
            {
                case MasteryNode.Power: return "Power";
                case MasteryNode.MaxHP: return "Max HP";
                case MasteryNode.HPRegen: return "HP Regen";
                case MasteryNode.AttackSpeed: return "Attack Speed";
                case MasteryNode.CritChance: return "Crit Chance";
                case MasteryNode.MoveSpeed: return "Move Speed";
                case MasteryNode.PickupRadius: return "Pickup Radius";
                default: return "Corruption Resist";
            }
        }

        void EnsureSize()
        {
            if (Levels == null || Levels.Length < 8)
            {
                var grown = new int[8];
                if (Levels != null) Array.Copy(Levels, grown, Math.Min(Levels.Length, 8));
                Levels = grown;
            }
        }

        public static IReadOnlyList<MasteryNode> AllNodes = new[]
        {
            MasteryNode.Power, MasteryNode.MaxHP, MasteryNode.HPRegen, MasteryNode.AttackSpeed,
            MasteryNode.CritChance, MasteryNode.MoveSpeed, MasteryNode.PickupRadius,
            MasteryNode.CorruptionResist
        };
    }
}
