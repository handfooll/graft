using System.Collections.Generic;
using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// Paces a single 3-6 minute run (GDD S3). Waves ramp in size and toughness, the enemy
    /// mix widens as the run goes on -- which is what keeps opening new graft options rather
    /// than just raising numbers -- and the run ends at a boss with a real attack pattern.
    /// </summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        public const int BossWave = 6;

        public int CurrentWave { get; private set; }
        public float TimeLeftInWave { get; private set; }
        public bool BossActive { get; private set; }
        public bool RunCleared { get; private set; }

        RunContext _ctx;
        Transform _enemyParent;
        float _spawnTimer;
        EnemyAgent _boss;
        BossPattern _bossPattern;

        static readonly float[] WaveDurations = { 26f, 28f, 30f, 32f, 34f, 999f };

        public void Init(RunContext ctx, Transform enemyParent)
        {
            _ctx = ctx;
            _enemyParent = enemyParent;
            CurrentWave = 0;
            AdvanceWave();
        }

        void Update()
        {
            if (_ctx == null || _ctx.Run.IsDead || RunCleared) return;
            float dt = Time.deltaTime;

            if (BossActive)
            {
                if (_boss == null || !_boss.IsAlive)
                {
                    BossActive = false;
                    RunCleared = true;
                }
                return;
            }

            TimeLeftInWave -= dt;
            _spawnTimer -= dt;

            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SpawnInterval();
                SpawnBatch();
            }

            if (TimeLeftInWave <= 0f) AdvanceWave();
        }

        void AdvanceWave()
        {
            CurrentWave++;
            _ctx.Run.Wave = CurrentWave;
            TimeLeftInWave = WaveDurations[Mathf.Min(CurrentWave - 1, WaveDurations.Length - 1)];
            _spawnTimer = 0.35f;

            if (CurrentWave >= BossWave) SpawnBoss();
        }

        float SpawnInterval() => Mathf.Max(0.35f, 1.5f - 0.16f * (CurrentWave - 1));

        int BatchSize() => Mathf.Clamp(1 + CurrentWave / 2, 1, 6);

        float HPScale() => 1f + 0.28f * (CurrentWave - 1);

        /// <summary>
        /// The mix widens with the wave: wave 1 is one family, so the first graft offer is
        /// predictable and teaches the mechanic; later waves mix everything.
        /// </summary>
        IReadOnlyList<EnemyArchetype> PoolForWave()
        {
            switch (CurrentWave)
            {
                case 1: return new[] { ArchetypeCatalog.Venomspitter };
                case 2: return new[] { ArchetypeCatalog.Venomspitter, ArchetypeCatalog.Sparkmite };
                case 3: return new[] { ArchetypeCatalog.Venomspitter, ArchetypeCatalog.Sparkmite,
                                       ArchetypeCatalog.Bloodcharger };
                default: return ArchetypeCatalog.WaveSpawnable;
            }
        }

        void SpawnBatch()
        {
            var pool = PoolForWave();
            int n = BatchSize();
            for (int i = 0; i < n; i++)
            {
                var archetype = pool[Random.Range(0, pool.Count)];
                SpawnOne(archetype, HPScale(), isBoss: false);
            }
        }

        EnemyAgent SpawnOne(EnemyArchetype archetype, float hpScale, bool isBoss)
        {
            var go = new GameObject((isBoss ? "Boss_" : "Enemy_") + archetype.Id);
            go.transform.SetParent(_enemyParent, false);
            go.transform.position = EdgePosition();

            var agent = go.AddComponent<EnemyAgent>();
            agent.Init(_ctx, archetype, hpScale, isBoss);
            return agent;
        }

        /// <summary>Spawn just outside the playable rectangle so nothing appears on top of
        /// the player.</summary>
        Vector2 EdgePosition()
        {
            var b = Arena.Instance.Bounds;
            float margin = 1.2f;
            int side = Random.Range(0, 4);
            switch (side)
            {
                case 0: return new Vector2(Random.Range(-b.x, b.x), b.y + margin);
                case 1: return new Vector2(Random.Range(-b.x, b.x), -b.y - margin);
                case 2: return new Vector2(-b.x - margin, Random.Range(-b.y, b.y));
                default: return new Vector2(b.x + margin, Random.Range(-b.y, b.y));
            }
        }

        void SpawnBoss()
        {
            BossActive = true;
            Arena.Instance.ClearAll();
            _boss = SpawnOne(ArchetypeCatalog.HollowChoir, 1f, isBoss: true);
            _boss.transform.position = new Vector2(0f, Arena.Instance.Bounds.y * 0.6f);
            _bossPattern = _boss.gameObject.AddComponent<BossPattern>();
            _bossPattern.Init(_ctx, _boss);
        }
    }

    /// <summary>
    /// The boss is a pattern, not a stat check (GDD S8). It alternates a radial burst you
    /// walk out of and a summon that restocks the arena -- which also keeps essence flowing
    /// so the fight can still change your build.
    /// </summary>
    public sealed class BossPattern : MonoBehaviour
    {
        RunContext _ctx;
        EnemyAgent _self;
        float _timer = 3f;
        int _step;

        public void Init(RunContext ctx, EnemyAgent self)
        {
            _ctx = ctx;
            _self = self;
        }

        void Update()
        {
            if (_self == null || !_self.IsAlive || _ctx.Run.IsDead) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            if (_step % 2 == 0)
            {
                RadialBurst();
                _timer = 4.5f;
            }
            else
            {
                SummonAdds();
                _timer = 6f;
            }
            _step++;
        }

        void RadialBurst()
        {
            var color = Palette.For(Element.Hollow);
            AbilityFx.SpawnRing(transform.position, 5.5f, color);

            // Telegraphed by the expanding ring: damage lands slightly after, so standing
            // still is punished but reacting is always possible.
            StartCoroutine(BurstAfterDelay(0.3f));
        }

        System.Collections.IEnumerator BurstAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            var player = Arena.Instance.Player;
            if (player == null || _ctx.Run.IsDead) yield break;

            float dist = Vector2.Distance(player.position, transform.position);
            if (dist <= 5.5f) _ctx.DamagePlayer(22f);
        }

        void SummonAdds()
        {
            for (int i = 0; i < 4; i++)
            {
                var archetype = ArchetypeCatalog.WaveSpawnable[
                    Random.Range(0, ArchetypeCatalog.WaveSpawnable.Count)];

                var go = new GameObject("BossAdd_" + archetype.Id);
                go.transform.SetParent(transform.parent, false);
                float angle = i / 4f * Mathf.PI * 2f;
                go.transform.position = (Vector2)transform.position +
                                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.2f;

                go.AddComponent<EnemyAgent>().Init(_ctx, archetype, 1.6f, false);
            }
        }
    }
}
