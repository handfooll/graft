using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// One mote of essence lying in the world, or on its way to the player.
    ///
    /// Deliberately passive: it holds its own state and draws itself, and <see cref="EssenceField"/>
    /// advances it. Two hundred motes each running their own <c>Update</c> is two hundred managed
    /// calls a frame for arithmetic that fits on one line, and D8 makes the phone the machine that
    /// pays for that - the same reason shots and bursts are pooled and ticked centrally.
    ///
    /// No collider. Nothing collides with essence; the magnet works on distance, and a collider
    /// here would also punch a hole in the NavMesh bake wherever a mote happened to be lying.
    /// </summary>
    public class EssenceDrop : MonoBehaviour
    {
        /// <summary>Metres from the player at which a flying mote counts as arrived.</summary>
        const float ArrivalRadius = 0.35f;

        /// <summary>How far a resting mote bobs, and how fast. Small - it is a 26 cm cube.</summary>
        const float BobHeight = 0.09f;
        const float BobSpeed = 2.4f;
        const float SpinSpeed = 95f;

        Transform _visual;
        MeshRenderer _renderer;

        EssenceMote _mote;
        Vector3 _restPosition;
        float _age;
        float _lifetime;
        bool _flying;

        public EssenceMote Mote => _mote;

        /// <summary>On the ground and unclaimed. What a full cargo bar looks like from the air.</summary>
        public bool IsResting => isActiveAndEnabled && !_flying;

        public bool IsFlying => _flying;

        void Awake()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mote";
            cube.transform.SetParent(transform, false);
            Destroy(cube.GetComponent<Collider>());

            _visual = cube.transform;
            _renderer = cube.GetComponent<MeshRenderer>();
            // Motes do not cast shadows. There can be a dozen on the ground at once and each one
            // would throw a hard little rectangle beside itself under a 38-degree sun - the same
            // mistake the boundary wall made in Phase 2, in miniature and multiplied.
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        /// <summary>Puts this mote on the ground at <paramref name="at"/> and starts its clock.</summary>
        public void Place(EssenceMote mote, Vector3 at, float size, float lifetime, Material material)
        {
            _mote = mote;
            _restPosition = at;
            _age = 0f;
            _lifetime = Mathf.Max(0.5f, lifetime);
            _flying = false;

            transform.position = at;
            // Tilted, so the cube reads as a crystal rather than as a box aligned with the ground.
            _visual.localRotation = Quaternion.Euler(35f, 0f, 35f);
            _visual.localScale = Vector3.one * Mathf.Max(0.02f, size);
            _renderer.sharedMaterial = material;

            gameObject.SetActive(true);
        }

        public void BeginFlight() => _flying = true;

        /// <summary>
        /// Restyles a mote already on the ground. Used when the cargo bar fills or empties, so
        /// every unclaimable mote changes at once - a mote dropped before the bar filled and one
        /// dropped after must not look like different things, because they are not.
        /// </summary>
        public void SetMaterial(Material material)
        {
            if (material != null)
            {
                _renderer.sharedMaterial = material;
            }
        }

        /// <summary>Gives up on the flight and settles wherever it got to. Used when the bar fills mid-flight.</summary>
        public void Settle()
        {
            _flying = false;
            _restPosition = transform.position;
        }

        /// <summary>
        /// One step. Returns true when the mote has reached <paramref name="player"/> and should be
        /// absorbed - the caller does the absorbing, because whether there is room for it is the
        /// ledger's business and not this object's.
        /// </summary>
        public bool Advance(Vector3 player, float flightSpeed, float deltaTime)
        {
            _age += deltaTime;

            if (!_flying)
            {
                var bob = Mathf.Sin(_age * BobSpeed) * BobHeight;
                transform.position = _restPosition + Vector3.up * (BobHeight + bob);
                _visual.localRotation *= Quaternion.Euler(0f, SpinSpeed * deltaTime, 0f);
                return false;
            }

            // Aimed at chest height rather than at the player's feet, so a mote arriving does so
            // into the character instead of disappearing under it.
            var target = player + Vector3.up * 0.7f;
            var step = Mathf.Max(0.1f, flightSpeed) * deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target, step);
            _visual.localRotation *= Quaternion.Euler(0f, SpinSpeed * 2.5f * deltaTime, 0f);

            return (transform.position - target).sqrMagnitude <= ArrivalRadius * ArrivalRadius;
        }

        public bool Expired => _age >= _lifetime;

        public void Retire()
        {
            _flying = false;
            gameObject.SetActive(false);
        }
    }
}
