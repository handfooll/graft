using System;
using System.IO;
using UnityEngine;

namespace Graft.Core
{
    /// <summary>
    /// Reads and writes the profile as JSON. The directory is an explicit constructor
    /// argument rather than a global so a test can never write to the real player's file --
    /// the protection is structural instead of relying on run-mode detection.
    /// </summary>
    public sealed class SaveService
    {
        public const string FileName = "graft_profile.json";

        readonly string _directory;

        public SaveService(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("SaveService needs an explicit directory.", nameof(directory));
            _directory = directory;
        }

        public string FullPath => Path.Combine(_directory, FileName);

        public PlayerProfile Load()
        {
            try
            {
                if (!File.Exists(FullPath)) return PlayerProfile.NewProfile();
                var json = File.ReadAllText(FullPath);
                if (string.IsNullOrWhiteSpace(json)) return PlayerProfile.NewProfile();

                var profile = JsonUtility.FromJson<PlayerProfile>(json);
                if (profile == null) return PlayerProfile.NewProfile();

                // No migration system yet: tolerate a file written by an older schema rather
                // than assuming every field is present.
                if (profile.UnlockedVesselIds == null || profile.UnlockedVesselIds.Count == 0)
                    profile.UnlockedVesselIds = new System.Collections.Generic.List<string> { VesselCatalog.Scaleborn.Id };
                if (profile.Masteries == null)
                    profile.Masteries = new System.Collections.Generic.List<VesselMastery>();
                if (profile.Weapons == null || profile.Weapons.Count == 0)
                    return Reseed(profile);
                if (string.IsNullOrEmpty(profile.EquippedVesselId))
                    profile.EquippedVesselId = VesselCatalog.Scaleborn.Id;
                if (string.IsNullOrEmpty(profile.EquippedWeaponId))
                    profile.EquippedWeaponId = WeaponCatalog.RustEdge.Id;

                return profile;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GRAFT] Profile unreadable, starting fresh: " + e.Message);
                return PlayerProfile.NewProfile();
            }
        }

        static PlayerProfile Reseed(PlayerProfile profile)
        {
            profile.Weapons = new System.Collections.Generic.List<PlayerProfile.OwnedWeaponRecord>
            {
                new PlayerProfile.OwnedWeaponRecord
                {
                    DefinitionId = WeaponCatalog.RustEdge.Id, Tier = (int)Rarity.Common, Level = 1
                }
            };
            return profile;
        }

        public void Save(PlayerProfile profile)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(FullPath, JsonUtility.ToJson(profile, true));
            }
            catch (Exception e)
            {
                Debug.LogError("[GRAFT] Could not save profile: " + e.Message);
            }
        }

        public void Delete()
        {
            if (File.Exists(FullPath)) File.Delete(FullPath);
        }
    }
}
