using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Short-lived visual feedback for abilities. Every graft firing has to be legible at a
    /// glance or the build stops feeling like yours -- these are the cheapest shapes that
    /// still read as "that slot just did something".
    /// </summary>
    public static class AbilityFx
    {
        public static void SpawnArc(Vector3 at, Vector2 facing, float radius, float arcDegrees, Color color)
        {
            var go = new GameObject("FX_Arc");
            go.transform.position = at;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var sr = SpriteFactory.NewRenderer("Sweep", go.transform, SpriteFactory.Glow(),
                new Color(color.r, color.g, color.b, 0.55f), order: 4);
            // Squash the glow along the off-axis so a narrow arc looks narrow.
            float width = Mathf.Clamp01(arcDegrees / 360f);
            sr.transform.localScale = new Vector3(radius * 2f, radius * 2f * Mathf.Max(0.25f, width), 1f);

            go.AddComponent<FxFade>().Init(0.18f, sr);
        }

        public static void SpawnRing(Vector3 at, float radius, Color color)
        {
            var go = new GameObject("FX_Ring");
            go.transform.position = at;
            var sr = SpriteFactory.NewRenderer("Ring", go.transform, SpriteFactory.Ring(0.09f),
                new Color(color.r, color.g, color.b, 0.7f), order: 4);
            sr.transform.localScale = Vector3.one * radius * 0.4f;
            go.AddComponent<FxExpandFade>().Init(0.32f, sr, radius * 2f);
        }

        public static void SpawnChainBolt(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("FX_Bolt");
            Vector3 mid = (from + to) * 0.5f;
            go.transform.position = mid;
            Vector3 delta = to - from;
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var sr = SpriteFactory.NewRenderer("Bolt", go.transform, SpriteFactory.Square(),
                new Color(color.r, color.g, color.b, 0.85f), order: 6);
            sr.transform.localScale = new Vector3(delta.magnitude, 0.07f, 1f);
            go.AddComponent<FxFade>().Init(0.14f, sr);
        }

        sealed class FxFade : MonoBehaviour
        {
            float _life, _duration;
            SpriteRenderer _sr;
            Color _start;

            public void Init(float duration, SpriteRenderer sr)
            {
                _duration = duration; _sr = sr; _start = sr.color;
            }

            void Update()
            {
                _life += Time.deltaTime;
                float k = Mathf.Clamp01(_life / _duration);
                if (_sr != null) _sr.color = new Color(_start.r, _start.g, _start.b, _start.a * (1f - k));
                if (k >= 1f) Destroy(gameObject);
            }
        }

        sealed class FxExpandFade : MonoBehaviour
        {
            float _life, _duration, _targetScale;
            SpriteRenderer _sr;
            Color _start;
            float _startScale;

            public void Init(float duration, SpriteRenderer sr, float targetScale)
            {
                _duration = duration; _sr = sr; _start = sr.color;
                _targetScale = targetScale; _startScale = sr.transform.localScale.x;
            }

            void Update()
            {
                _life += Time.deltaTime;
                float k = Mathf.Clamp01(_life / _duration);
                if (_sr != null)
                {
                    _sr.color = new Color(_start.r, _start.g, _start.b, _start.a * (1f - k));
                    float s = Mathf.Lerp(_startScale, _targetScale, k);
                    _sr.transform.localScale = new Vector3(s, s, 1f);
                }
                if (k >= 1f) Destroy(gameObject);
            }
        }
    }
}
