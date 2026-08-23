using System.Collections.Generic;
using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// A single travelling hit. Firearm chassis grafts fire several of these in a spread;
    /// blade chassis grafts use the instant arc path instead and never spawn one.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        AbilitySpec _spec;
        float _damage;
        Vector2 _direction;
        float _travelled;
        float _maxRange;
        RunContext _ctx;
        readonly HashSet<EnemyAgent> _alreadyHit = new HashSet<EnemyAgent>();

        public static void Spawn(RunContext ctx, AbilitySpec spec, float damage, Vector2 origin,
            Vector2 direction, Transform parent)
        {
            var go = new GameObject("Shot");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;

            var p = go.AddComponent<Projectile>();
            p._ctx = ctx;
            p._spec = spec;
            p._damage = damage;
            p._direction = direction.normalized;
            p._maxRange = spec.Range;

            var color = Palette.For(spec.Element);
            var sr = SpriteFactory.NewRenderer("Mote", go.transform, SpriteFactory.Mote(), color, order: 6);
            sr.transform.localScale = Vector3.one * 0.22f;
            go.transform.right = p._direction;
        }

        void Update()
        {
            float step = _spec.ProjectileSpeed * Time.deltaTime;
            transform.position += (Vector3)(_direction * step);
            _travelled += step;

            var hit = Arena.Instance.Nearest(transform.position, 0.34f);
            if (hit != null && !_alreadyHit.Contains(hit))
            {
                _alreadyHit.Add(hit);
                CombatResolver.ApplyHit(_ctx, _spec, _damage, hit, _direction);
                Destroy(gameObject);
                return;
            }

            if (_travelled >= _maxRange) Destroy(gameObject);
        }
    }
}
