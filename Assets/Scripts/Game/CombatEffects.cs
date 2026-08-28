using System.Collections.Generic;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Everything a fight throws that is not an enemy: the shots in flight and the bursts where
    /// they land. One component owns both because they share a lifetime - a burst is what a
    /// projectile turns into - and because pooling them together keeps the whole of a fight's
    /// churn in one place.
    ///
    /// Pooled rather than instantiated. At the Ember Scar cap of ten enemies the player alone fires
    /// better than two shots a second, and iOS-first (D8) means the allocation this avoids is the
    /// kind that shows up as a stutter on the device long before it shows up in the editor.
    /// </summary>
    public class CombatEffects : MonoBehaviour
    {
        [SerializeField] CombatTuning tuning;

        readonly Stack<Projectile> _idleShots = new Stack<Projectile>();
        readonly List<Projectile> _liveShots = new List<Projectile>();
        readonly Stack<ImpactBurst> _idleBursts = new Stack<ImpactBurst>();

        Transform _shotRoot;
        Transform _burstRoot;

        public CombatTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        /// <summary>Shots currently in the air. Read by the tour to prove one is.</summary>
        public int LiveShotCount => _liveShots.Count;

        void Awake()
        {
            _shotRoot = new GameObject("Shots").transform;
            _shotRoot.SetParent(transform, false);
            _burstRoot = new GameObject("Bursts").transform;
            _burstRoot.SetParent(transform, false);
        }

        /// <summary>The player's auto-attack. Fires from chest height toward an enemy.</summary>
        public Projectile FirePlayerShot(Vector3 origin, Damageable target, float aimHeight, float damage)
        {
            return Fire(origin, target, aimHeight, damage, tuning.projectileSpeed,
                tuning.playerProjectileMaterial);
        }

        /// <summary>Spark's shot. Slower on purpose - it is meant to be seen coming.</summary>
        public Projectile FireEnemyShot(Vector3 origin, Damageable target, float aimHeight, float damage)
        {
            return Fire(origin, target, aimHeight, damage, tuning.enemyProjectileSpeed,
                tuning.enemyProjectileMaterial);
        }

        Projectile Fire(Vector3 origin, Damageable target, float aimHeight, float damage, float speed,
            Material material)
        {
            if (tuning == null || target == null || !target.IsAlive)
            {
                return null;
            }

            var shot = _idleShots.Count > 0 ? _idleShots.Pop() : NewShot();
            _liveShots.Add(shot);
            shot.Launch(origin, target, aimHeight, speed, damage, tuning.projectileRadius, material,
                Recycle, Burst);
            return shot;
        }

        void Recycle(Projectile shot)
        {
            _liveShots.Remove(shot);
            _idleShots.Push(shot);
        }

        void Burst(Vector3 at, bool connected)
        {
            // A shot that expired chasing something already dead leaves nothing behind. Drawing a
            // burst for it would put a flash in mid-air with no target under it, which reads as a
            // shot that missed - and nothing in this game can miss.
            if (!connected || tuning == null)
            {
                return;
            }

            var burst = _idleBursts.Count > 0 ? _idleBursts.Pop() : NewBurst();
            burst.Play(at, tuning.impactBurstRadius, tuning.hitFlashSeconds * 2.2f,
                tuning.hitFlashMaterial, ReturnBurst);
        }

        void ReturnBurst(ImpactBurst burst) => _idleBursts.Push(burst);

        Projectile NewShot()
        {
            var shot = new GameObject("Shot").AddComponent<Projectile>();
            shot.transform.SetParent(_shotRoot, false);
            shot.gameObject.SetActive(false);
            return shot;
        }

        ImpactBurst NewBurst()
        {
            var burst = new GameObject("Burst").AddComponent<ImpactBurst>();
            burst.transform.SetParent(_burstRoot, false);
            burst.gameObject.SetActive(false);
            return burst;
        }

        /// <summary>
        /// Puts every shot in the air away without letting it land. The screenshot tour poses
        /// fights one after another in a scene it does not reload between captures, and a shot left
        /// over from the previous pose would arrive during the next one.
        /// </summary>
        public void ClearShots()
        {
            for (var i = _liveShots.Count - 1; i >= 0; i--)
            {
                _liveShots[i].Cancel();
            }

            _liveShots.Clear();
        }
    }
}
