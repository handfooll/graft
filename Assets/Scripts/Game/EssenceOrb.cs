using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// Essence shaped like the thing that dropped it (GDD S4). It flies to the hero like an
    /// XP orb, but it fills that enemy family's own meter rather than a shared bar.
    /// </summary>
    public sealed class EssenceOrb : MonoBehaviour
    {
        EnemyArchetype _archetype;
        RunContext _ctx;
        Transform _target;
        float _speed;
        bool _magnetised;
        float _life;

        public static void Spawn(RunContext ctx, EnemyArchetype archetype, Vector3 at, Transform parent)
        {
            var go = new GameObject("Essence_" + archetype.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = at + (Vector3)(Random.insideUnitCircle * 0.28f);
            var orb = go.AddComponent<EssenceOrb>();
            orb._archetype = archetype;
            orb._ctx = ctx;
            orb._speed = 2.5f;

            var color = Palette.For(archetype.Element);
            var sr = SpriteFactory.NewRenderer("Mote", go.transform, SpriteFactory.Mote(), color, order: 3);
            sr.transform.localScale = Vector3.one * 0.24f;
            var glow = SpriteFactory.NewRenderer("Glow", go.transform, SpriteFactory.Glow(),
                new Color(color.r, color.g, color.b, 0.35f), order: 2);
            glow.transform.localScale = Vector3.one * 0.55f;
        }

        void Update()
        {
            _life += Time.deltaTime;
            _target = Arena.Instance != null ? Arena.Instance.Player : null;
            if (_target == null) return;

            Vector2 delta = (Vector2)_target.position - (Vector2)transform.position;
            float dist = delta.magnitude;

            float pickup = _ctx.Run.BaseStats.PickupRadius;
            // A short arming delay keeps the drop readable before it flies off.
            if (!_magnetised && _life > 0.15f && dist <= pickup) _magnetised = true;

            if (_magnetised)
            {
                _speed = Mathf.Min(_speed + Time.deltaTime * 22f, 18f);
                transform.position = Vector2.MoveTowards(transform.position, _target.position,
                    _speed * Time.deltaTime);
                if (dist < 0.3f) Collect();
            }
            else
            {
                // Idle bob so a field of uncollected drops still feels alive.
                transform.position += (Vector3)(Vector2.up * Mathf.Sin(Time.time * 3f + _life) * 0.12f * Time.deltaTime);
            }
        }

        void Collect()
        {
            _ctx.Run.RegisterKill(_archetype, 1);
            Destroy(gameObject);
        }
    }
}
