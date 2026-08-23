using System;
using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// Everything that survives between runs (GDD S3 meta loop, S8 currencies).
    /// Plain serializable data -- JsonUtility cannot handle dictionaries, hence the lists.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public int SoftGold = 250;
        public int WeaponCores = 0;
        public int VesselShards = 0;
        public int PrimeEssence = 0;

        public int HighestWaveCleared = 0;
        public int TotalRuns = 0;

        public string EquippedVesselId = VesselCatalog.Scaleborn.Id;
        public string EquippedWeaponId = WeaponCatalog.RustEdge.Id;
        public int EquippedWeaponTier = (int)Rarity.Common;

        public List<string> UnlockedVesselIds = new List<string> { VesselCatalog.Scaleborn.Id };
        public List<VesselMastery> Masteries = new List<VesselMastery>();
        public List<OwnedWeaponRecord> Weapons = new List<OwnedWeaponRecord>();

        [Serializable]
        public sealed class OwnedWeaponRecord
        {
            public string DefinitionId;
            public int Tier;
            public int Level = 1;
        }

        public bool HasVessel(string vesselId) => UnlockedVesselIds.Contains(vesselId);

        public VesselMastery MasteryFor(string vesselId)
        {
            for (int i = 0; i < Masteries.Count; i++)
            {
                if (Masteries[i].VesselId == vesselId) return Masteries[i];
            }
            var m = new VesselMastery(vesselId);
            Masteries.Add(m);
            return m;
        }

        public List<OwnedWeapon> WeaponStack()
        {
            var list = new List<OwnedWeapon>(Weapons.Count);
            for (int i = 0; i < Weapons.Count; i++)
            {
                list.Add(new OwnedWeapon
                {
                    DefinitionId = Weapons[i].DefinitionId,
                    Tier = (Rarity)Weapons[i].Tier,
                    Level = Weapons[i].Level
                });
            }
            return list;
        }

        public void StoreWeaponStack(List<OwnedWeapon> stack)
        {
            Weapons.Clear();
            for (int i = 0; i < stack.Count; i++)
            {
                Weapons.Add(new OwnedWeaponRecord
                {
                    DefinitionId = stack[i].DefinitionId,
                    Tier = (int)stack[i].Tier,
                    Level = stack[i].Level
                });
            }
        }

        public OwnedWeapon EquippedWeapon()
        {
            var stack = WeaponStack();
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i].DefinitionId == EquippedWeaponId && (int)stack[i].Tier == EquippedWeaponTier)
                    return stack[i];
            }
            return new OwnedWeapon
            {
                DefinitionId = EquippedWeaponId,
                Tier = (Rarity)EquippedWeaponTier,
                Level = 1
            };
        }

        /// <summary>A brand-new player: one body, one blade, one sidearm to merge toward.</summary>
        public static PlayerProfile NewProfile()
        {
            var p = new PlayerProfile();
            p.Weapons.Add(new OwnedWeaponRecord
            {
                DefinitionId = WeaponCatalog.RustEdge.Id, Tier = (int)Rarity.Common, Level = 1
            });
            p.Weapons.Add(new OwnedWeaponRecord
            {
                DefinitionId = WeaponCatalog.SlugThrower.Id, Tier = (int)Rarity.Common, Level = 1
            });
            p.MasteryFor(VesselCatalog.Scaleborn.Id);
            return p;
        }
    }
}
