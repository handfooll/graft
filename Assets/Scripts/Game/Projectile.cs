using System;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// One shot in flight. The player's auto-attack fires these, and so does Spark.
    ///
    /// **Why a projectile at all, rather than applying the damage the instant the clock says so.**
    /// Instant damage is cheaper and it is also invisible: the enemy's health simply drops, and
    /// nothing on screen says who did it or when. A travelling shot is the only part of a fight
    /// that shows the fight happening - it is what makes "a hit landing" something a screenshot can
    /// contain, which is exactly what Phase 3's gate asks for.
    ///
    /// It homes rather than travelling ballistically. Leading a target is not the skill this game
    /// is testing - the stick is one thumb and the attack is automatic - so a shot that can miss
    /// would only ever read as the auto-attack being broken.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        /// <summary>
        /// A shot cannot live longer than this. Its target can be recycled by the spawner mid
        /// flight, and a homing projectile chasing a destination that no longer updates would
        /// otherwise sit exactly where it was when the target vanished, forever.
        /// </summary>
        const float MaxLifetime = 4f;

        Transform _visual;
        MeshRenderer _renderer;

        Damageable _target;
        Vector3 _destination;
        float _speed;
        float _damage;
        float _hitRadius;
        float _aimHeight;
        float _age;
        bool _inFlight;

        Action<Projectile> _onFinished;
        Action<Vector3, bool> _onImpact;

        public bool InFlight => _inFlight;

        /// <summary>The thing this shot is going to hurt. Null once it has been recycled.</summary>
        public Damageable Target => _target;

        void Awake()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Bolt";
            sphere.transform.SetParent(transform, false);
            Destroy(sphere.GetComponent<Collider>());

            _visual = sphere.transform;
            _renderer = sphere.GetComponent<MeshRenderer>();
            // A shot is a moving light, not a moving object. Its own shadow scudding across the
            // ground reads as a bird passing overhead and pulls the eye off the fight.
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        /// <summary>
        /// Sends this shot at <paramref name="target"/>.
        /// </summary>
        /// <param name="onImpact">
        /// Called wherever the shot ends, with whether it actually hit something. The pool draws
        /// the impact burst from this, so a shot that expired chasing a corpse leaves no flash.
        /// </param>
        public void Launch(Vector3 origin, Damageable target, float aimHeight, float speed, float damage,
            float radius, Material material, Action<Projectile> onFinished, Action<Vector3, bool> onImpact)
        {
            transform.position = origin;
            _visual.localScale = Vector3.one * (radius * 2f);
            _renderer.sharedMaterial = material;

            _target = target;
            _aimHeight = Mathf.Max(0.2f, aimHeight);
            _destination = AimPoint(target, _aimHeight, origin);
            _speed = Mathf.Max(1f, speed);
            _damage = Mathf.Max(0f, damage);
            // The bolt has a real width, so it should connect when its surface touches rather than
            // when its centre does. Without the radius a fat slow bolt visibly overlaps the target
            // for a frame or two before anything happens.
            _hitRadius = Mathf.Max(0.15f, radius + 0.35f);
            _age = 0f;
            _inFlight = true;
            _onFinished = onFinished;
            _onImpact = onImpact;

            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_inFlight)
            {
                return;
            }

            _age += Time.deltaTime;

            var alive = _target != null && _target.IsAlive && _target.isActiveAndEnabled;
            if (alive)
            {
                // Re-aimed every frame at a live target, and frozen at the last known point once
                // the target is gone, so a shot already in the air still arrives somewhere and
                // still draws its burst there.
                _destination = AimPoint(_target, _aimHeight, _destination);
            }

            var step = _speed * Time.deltaTime;
            var toTarget = _destination - transform.position;
            var distance = toTarget.magnitude;

            if (distance <= Mathf.Max(step, _hitRadius))
            {
                Arrive(alive);
                return;
            }

            transform.position += toTarget * (step / distance);

            if (_age >= MaxLifetime)
            {
                Arrive(false);
            }
        }

        void Arrive(bool hitSomething)
        {
            var landed = _destination;
            var connected = false;

            if (hitSomething && _target != null)
            {
                _target.ApplyDamage(_damage);
                connected = true;
            }

            Finish();
            _onImpact?.Invoke(landed, connected);
        }

        /// <summary>Puts the shot away without it landing. Used when the field is cleared.</summary>
        public void Cancel() => Finish();

        void Finish()
        {
            if (!_inFlight)
            {
                return;
            }

            _inFlight = false;
            _target = null;
            gameObject.SetActive(false);

            var finished = _onFinished;
            _onFinished = null;
            _onImpact = null;
            finished?.Invoke(this);
        }

        /// <summary>
        /// Where on a body to aim. Partway up rather than at the feet: a shot converging on ground
        /// level disappears behind the target's own body for the last half metre, and the impact
        /// then happens somewhere the player cannot see.
        /// </summary>
        static Vector3 AimPoint(Damageable target, float aimHeight, Vector3 fallback) =>
            target != null
                ? target.transform.position + Vector3.up * Mathf.Max(0.2f, aimHeight)
                : fallback;
    }
}
