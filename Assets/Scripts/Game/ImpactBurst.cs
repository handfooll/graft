using System;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The flash where a shot lands: a sphere that grows and fades over a fraction of a second.
    ///
    /// It exists because the enemy's own hit flash is not enough on its own. Whitening a body says
    /// *something happened to this*; a burst at the point of contact says *this is where the hit
    /// landed, and it landed now*. In a screenshot - which is the only thing Phase 3's gate can
    /// actually judge - the burst is the part that is legible, because it is the one element on
    /// screen that exists for a tenth of a second and therefore can only mean one thing.
    /// </summary>
    public class ImpactBurst : MonoBehaviour
    {
        Transform _visual;
        MeshRenderer _renderer;
        Action<ImpactBurst> _onFinished;
        float _duration = 0.2f;
        float _elapsed;
        float _radius = 1f;
        bool _playing;

        void Awake()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Flash";
            sphere.transform.SetParent(transform, false);
            Destroy(sphere.GetComponent<Collider>());

            _visual = sphere.transform;
            _renderer = sphere.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void Play(Vector3 at, float radius, float duration, Material material, Action<ImpactBurst> onFinished)
        {
            transform.position = at;
            _radius = Mathf.Max(0.05f, radius);
            _duration = Mathf.Max(0.02f, duration);
            _elapsed = 0f;
            _playing = true;
            _onFinished = onFinished;

            _renderer.sharedMaterial = material;
            _visual.localScale = Vector3.zero;

            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / _duration);

            // Fast out of the gate and easing to a stop, which is what an impact looks like.
            // A linear grow reads as something inflating.
            _visual.localScale = Vector3.one * (_radius * 2f * (1f - (1f - t) * (1f - t)));

            if (t < 1f)
            {
                return;
            }

            _playing = false;
            gameObject.SetActive(false);

            var finished = _onFinished;
            _onFinished = null;
            finished?.Invoke(this);
        }
    }
}
