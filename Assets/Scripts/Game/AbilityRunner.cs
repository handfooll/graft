using System.Collections.Generic;
using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// Fires whatever is installed in each of the five slots. Everything auto-attacks, which
    /// is what keeps the game one-thumb (GDD pillar 3) -- the player's only inputs are where
    /// to stand, when to dash, and which slot to graft into.
    /// </summary>
    public sealed class AbilityRunner : MonoBehaviour
    {
        RunContext _ctx;
        Transform _fxParent;
        PlayerController _player;

        readonly Dictionary<GraftSlot, float> _cooldowns = new Dictionary<GraftSlot, float>();
        readonly List<EnemyAgent> _scratch = new List<EnemyAgent>(64);

        public void Init(RunContext ctx, PlayerController player, Transform fxParent)
        {
            _ctx = ctx;
            _player = player;
            _fxParent = fxParent;
            foreach (var slot in RunState.AllSlots) _cooldowns[slot] = 0f;
        }

        void Update()
        {
            if (_ctx == null || _ctx.Run.IsDead) return;

            float dt = Time.deltaTime;
            float haste = Mathf.Max(0.25f, _ctx.Run.BaseStats.AttackSpeed);

            foreach (var slot in RunState.AllSlots)
            {
                // Legs bank their charge for the dash rather than firing on a timer.
                if (slot == GraftSlot.Legs) { _cooldowns[slot] -= dt * haste; continue; }

                var maybe = _ctx.Run.SpecInSlot(slot);
                if (!maybe.HasValue) continue;

                _cooldowns[slot] -= dt * haste;
                if (_cooldowns[slot] > 0f) continue;

                var spec = maybe.Value;
                if (TryFire(slot, spec)) _cooldowns[slot] = spec.Cooldown;
            }
        }

        /// <summary>Called by the controller the moment a dash starts.</summary>
        public void OnDash(Vector2 direction)
        {
            var maybe = _ctx.Run.SpecInSlot(GraftSlot.Legs);
            if (!maybe.HasValue) return;
            if (_cooldowns[GraftSlot.Legs] > 0f) return;

            var spec = maybe.Value;
            _cooldowns[GraftSlot.Legs] = spec.Cooldown;
            FireArc(spec, direction, dashOrigin: true);
        }

        bool TryFire(GraftSlot slot, AbilitySpec spec)
        {
            switch (spec.Delivery)
            {
                case Delivery.Aimed:
                {
                    var target = Arena.Instance.Nearest(transform.position, spec.Range);
                    if (target == null) return false;
                    Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                    FireDirectional(spec, dir);
                    return true;
                }
                case Delivery.Swipe:
                {
                    // Only swing if something is actually in reach, so arms are not
                    // permanently flailing at nothing.
                    Vector2 facing = FacingFor(slot);
                    Arena.Instance.CollectInArc(transform.position, facing, spec.Range, spec.ArcDegrees, _scratch);
                    if (_scratch.Count == 0) return false;
                    FireArc(spec, facing, dashOrigin: false);
                    return true;
                }
                case Delivery.Aura:
                {
                    Arena.Instance.CollectInRadius(transform.position, spec.Range, _scratch);
                    if (_scratch.Count == 0) return false;
                    FirePulse(spec);
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Arms swing toward the nearest threat on their own side, so two arm
        /// grafts cover more ground than one pointed the same way.</summary>
        Vector2 FacingFor(GraftSlot slot)
        {
            var nearest = Arena.Instance.Nearest(transform.position, 12f);
            Vector2 baseFacing = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : _player.Facing;

            if (slot == GraftSlot.LeftArm) return Rotate(baseFacing, 32f);
            if (slot == GraftSlot.RightArm) return Rotate(baseFacing, -32f);
            return baseFacing;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        float RollDamage(AbilitySpec spec)
        {
            // Power scales everything relative to the baseline of 10, so mastery and weapon
            // investment lift every graft rather than only the newest one.
            float scalar = _ctx.Run.EffectivePower / 10f;
            float dmg = spec.Damage * scalar;
            if (Random.value < _ctx.Run.BaseStats.CritChance) dmg *= 2f;
            return dmg;
        }

        void FireDirectional(AbilitySpec spec, Vector2 dir)
        {
            float damage = RollDamage(spec);

            if (spec.ProjectileCount > 1)
            {
                // Firearm chassis: a spread of real travelling shots.
                float spread = spec.ArcDegrees;
                for (int i = 0; i < spec.ProjectileCount; i++)
                {
                    float t = spec.ProjectileCount == 1 ? 0.5f : i / (float)(spec.ProjectileCount - 1);
                    float offset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                    Projectile.Spawn(_ctx, spec, damage, transform.position, Rotate(dir, offset), _fxParent);
                }
            }
            else
            {
                Projectile.Spawn(_ctx, spec, damage, transform.position, dir, _fxParent);
            }
        }

        void FireArc(AbilitySpec spec, Vector2 facing, bool dashOrigin)
        {
            float damage = RollDamage(spec);
            var color = Palette.For(spec.Element);

            if (spec.ProjectileCount > 1 && !dashOrigin)
            {
                FireDirectional(spec, facing);
                return;
            }

            Arena.Instance.CollectInArc(transform.position, facing, spec.Range, spec.ArcDegrees, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Vector2 dir = ((Vector2)_scratch[i].transform.position - (Vector2)transform.position).normalized;
                CombatResolver.ApplyHit(_ctx, spec, damage, _scratch[i], dir);
            }
            AbilityFx.SpawnArc(transform.position, facing, spec.Range, spec.ArcDegrees, color);
        }

        void FirePulse(AbilitySpec spec)
        {
            float damage = RollDamage(spec);
            Arena.Instance.CollectInRadius(transform.position, spec.Range, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Vector2 dir = ((Vector2)_scratch[i].transform.position - (Vector2)transform.position).normalized;
                CombatResolver.ApplyHit(_ctx, spec, damage, _scratch[i], dir);
            }
            AbilityFx.SpawnRing(transform.position, spec.Range, Palette.For(spec.Element));
        }
    }
}
