using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// One wave enemy. Chases the hero, damages on contact, and on death drops Essence
    /// shaped like its own family -- the drop is the whole point of the enemy existing.
    /// </summary>
    public sealed class EnemyAgent : MonoBehaviour
    {
        public EnemyArchetype Archetype { get; private set; }
        public bool IsAlive => _hp > 0f;
        public float CurrentHP => _hp;
        public float MaxHP => _maxHp;
        public float HealthFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;
        public bool IsBoss { get; private set; }

        float _hp;
        float _maxHp;
        float _contactCooldown;
        SpriteRenderer _sprite;
        SpriteRenderer _healthBar;
        Transform _healthBarRoot;
        Vector2 _knockback;

        // Damage over time left by venom grafts.
        float _dotRemaining;
        float _dotPerSecond;

        RunContext _ctx;
        float _size;

        public void Init(RunContext ctx, EnemyArchetype archetype, float hpScale, bool isBoss = false)
        {
            _ctx = ctx;
            Archetype = archetype;
            IsBoss = isBoss;

            _maxHp = archetype.BaseHP * hpScale;
            _hp = _maxHp;

            float size = isBoss ? 1.9f : Mathf.Lerp(0.42f, 0.78f, Mathf.InverseLerp(14f, 60f, archetype.BaseHP));
            _size = size;

            var color = Palette.For(archetype.Element);
            _sprite = SpriteFactory.NewRenderer("Body", transform,
                archetype.Element == Element.Hide ? SpriteFactory.Square() : SpriteFactory.Spiked(5),
                color, order: 5);
            _sprite.transform.localScale = Vector3.one * size;

            if (isBoss)
            {
                var ring = SpriteFactory.NewRenderer("BossRing", transform, SpriteFactory.Ring(0.10f),
                    new Color(1f, 1f, 1f, 0.55f), order: 6);
                ring.transform.localScale = Vector3.one * (size * 1.35f);
            }

            _healthBarRoot = new GameObject("HP").transform;
            _healthBarRoot.SetParent(transform, false);
            _healthBarRoot.localPosition = new Vector3(0f, size * 0.75f, 0f);
            var back = SpriteFactory.NewRenderer("Back", _healthBarRoot, SpriteFactory.Square(),
                new Color(0f, 0f, 0f, 0.6f), order: 7);
            back.transform.localScale = new Vector3(size * 1.1f, 0.08f, 1f);
            _healthBar = SpriteFactory.NewRenderer("Fill", _healthBarRoot, SpriteFactory.Square(),
                Color.white, order: 8);
            _healthBar.transform.localScale = new Vector3(size * 1.1f, 0.08f, 1f);
            _healthBarRoot.gameObject.SetActive(isBoss);

            Arena.Instance.Register(this);
        }

        void Update()
        {
            if (!IsAlive) return;
            float dt = Time.deltaTime;

            if (_dotRemaining > 0f)
            {
                _dotRemaining -= dt;
                ApplyDamage(_dotPerSecond * dt, showFlash: false);
                if (!IsAlive) return;
            }

            var player = Arena.Instance.Player;
            if (player == null) return;

            Vector2 pos = transform.position;
            Vector2 toPlayer = (Vector2)player.position - pos;
            float dist = toPlayer.magnitude;

            Vector2 step = Vector2.zero;
            if (dist > 0.01f) step = toPlayer / dist * Archetype.MoveSpeed * dt;

            // Knockback decays fast so an Impact graft shoves rather than launches.
            if (_knockback.sqrMagnitude > 0.001f)
            {
                step += _knockback * dt;
                _knockback = Vector2.Lerp(_knockback, Vector2.zero, dt * 7f);
            }

            transform.position = Arena.Instance.Clamp(pos + step);

            _contactCooldown -= dt;
            float touchRadius = IsBoss ? 1.15f : 0.55f;
            if (dist < touchRadius && _contactCooldown <= 0f)
            {
                _contactCooldown = 0.7f;
                _ctx.DamagePlayer(Archetype.ContactDamage);
            }
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (IsBoss) force *= 0.25f;   // a boss should not be trivially kited
            _knockback += direction.normalized * force;
        }

        public void ApplyDot(float perSecond, float duration)
        {
            // Refresh rather than stack, so venom stays a steady pressure not a burst.
            _dotPerSecond = Mathf.Max(_dotPerSecond, perSecond);
            _dotRemaining = Mathf.Max(_dotRemaining, duration);
        }

        public void ApplyDamage(float amount, bool showFlash = true)
        {
            if (!IsAlive || amount <= 0f) return;
            _hp -= amount;

            if (_healthBar != null)
            {
                float f = Mathf.Clamp01(_hp / _maxHp);
                // Shrink from the centre is wrong for a bar, so offset by the lost half.
                float full = _size * 1.1f;
                _healthBar.transform.localScale = new Vector3(full * f, 0.08f, 1f);
                _healthBar.transform.localPosition = new Vector3(-full * (1f - f) * 0.5f, 0f, 0f);
                _healthBar.color = Color.Lerp(Palette.Health, Color.white, f * 0.3f);
            }

            if (showFlash && _sprite != null) StartCoroutine(Flash());

            if (_hp <= 0f) Die();
        }

        System.Collections.IEnumerator Flash()
        {
            var original = _sprite.color;
            _sprite.color = Color.white;
            yield return new WaitForSeconds(0.04f);
            if (_sprite != null) _sprite.color = original;
        }

        void Die()
        {
            _hp = 0f;
            Arena.Instance.Unregister(this);
            _ctx.OnEnemyKilled(this);
            Destroy(gameObject);
        }
    }
}
