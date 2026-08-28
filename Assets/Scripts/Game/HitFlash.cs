using System.Collections.Generic;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Whitens an enemy for a few frames when it is hit, and fades it out as it dies.
    ///
    /// Done with a <see cref="MaterialPropertyBlock"/> rather than by swapping materials. Swapping
    /// gives every enemy its own material instance, which breaks batching, leaks a material per
    /// spawn, and - the reason that actually matters here - makes the palette in
    /// <see cref="EnemyRoster"/> stop being the thing on screen, because each instance drifts from
    /// the asset it was copied from.
    ///
    /// The flash is deliberately short (<c>hitFlashSeconds</c>, ~0.09 s). A swarm under sustained
    /// fire flashes constantly, and a flash long enough to overlap the next shot turns four enemies
    /// into one white blob.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<Color> _restingColours = new List<Color>();

        MaterialPropertyBlock _block;
        float _flashSeconds = 0.09f;
        float _remaining;
        float _dim = 1f;

        /// <summary>
        /// Re-reads the renderers under <paramref name="parts"/> and their resting colours. Called
        /// every time a pooled enemy is rebuilt, because the parts are new objects each time and
        /// the family - and therefore the palette - may have changed.
        /// </summary>
        public void Bind(Transform parts, float flashSeconds)
        {
            _block ??= new MaterialPropertyBlock();
            _flashSeconds = Mathf.Max(0.01f, flashSeconds);
            _remaining = 0f;
            _dim = 1f;

            _renderers.Clear();
            _restingColours.Clear();

            parts.GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
            {
                var material = renderer.sharedMaterial;
                _restingColours.Add(material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.grey);
            }

            Apply();
        }

        public void Flash()
        {
            _remaining = _flashSeconds;
            Apply();
        }

        /// <summary>
        /// How lit the body is overall, 1 resting and 0 gone. Driven down over
        /// <c>deathSeconds</c> so a kill dims as it collapses instead of vanishing between frames.
        /// </summary>
        public void SetDim(float dim)
        {
            _dim = Mathf.Clamp01(dim);
            Apply();
        }

        void Update()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            if (_block == null || _renderers.Count == 0)
            {
                return;
            }

            var flash = _flashSeconds > 0f ? Mathf.Clamp01(_remaining / _flashSeconds) : 0f;

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                // Toward white on a hit, toward black as it dies. Both are lerps off the resting
                // colour rather than absolute values, so an enemy mid-flash still reads as its own
                // family instead of as a generic white shape.
                var colour = Color.Lerp(_restingColours[i], Color.white, flash * 0.85f);
                colour = Color.Lerp(Color.black, colour, _dim);

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, colour);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
