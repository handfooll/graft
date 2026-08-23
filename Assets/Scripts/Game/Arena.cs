using System.Collections.Generic;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// A plain registry of live combatants. A survivor game queries "what is near me"
    /// constantly; keeping our own list and doing squared-distance checks is both simpler
    /// and cheaper than physics overlap queries at these counts, and it stays deterministic.
    /// </summary>
    public sealed class Arena : MonoBehaviour
    {
        public static Arena Instance { get; private set; }

        public readonly List<EnemyAgent> Enemies = new List<EnemyAgent>();
        public Transform Player { get; set; }

        /// <summary>Half-extents of the playable rectangle.</summary>
        public Vector2 Bounds = new Vector2(8.5f, 14f);

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(EnemyAgent e) => Enemies.Add(e);

        public void Unregister(EnemyAgent e) => Enemies.Remove(e);

        public Vector2 Clamp(Vector2 p) => new Vector2(
            Mathf.Clamp(p.x, -Bounds.x, Bounds.x),
            Mathf.Clamp(p.y, -Bounds.y, Bounds.y));

        public EnemyAgent Nearest(Vector2 from, float maxRange, EnemyAgent exclude = null)
        {
            EnemyAgent best = null;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.IsAlive || e == exclude) continue;
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = e; }
            }
            return best;
        }

        public void CollectInRadius(Vector2 center, float radius, List<EnemyAgent> into)
        {
            into.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.IsAlive) continue;
                if (((Vector2)e.transform.position - center).sqrMagnitude <= r2) into.Add(e);
            }
        }

        /// <summary>Everything inside a cone: used by swipes, auras (360) and dashes.</summary>
        public void CollectInArc(Vector2 center, Vector2 facing, float radius, float arcDegrees,
            List<EnemyAgent> into)
        {
            into.Clear();
            float r2 = radius * radius;
            float halfCos = Mathf.Cos(Mathf.Deg2Rad * arcDegrees * 0.5f);
            bool fullCircle = arcDegrees >= 359f;
            Vector2 f = facing.sqrMagnitude < 0.0001f ? Vector2.up : facing.normalized;

            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.IsAlive) continue;
                Vector2 delta = (Vector2)e.transform.position - center;
                if (delta.sqrMagnitude > r2) continue;
                if (!fullCircle)
                {
                    if (delta.sqrMagnitude < 0.0001f) { into.Add(e); continue; }
                    if (Vector2.Dot(delta.normalized, f) < halfCos) continue;
                }
                into.Add(e);
            }
        }

        public int AliveCount()
        {
            int n = 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (Enemies[i] != null && Enemies[i].IsAlive) n++;
            }
            return n;
        }

        public void ClearAll()
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                if (Enemies[i] != null) Destroy(Enemies[i].gameObject);
            }
            Enemies.Clear();
        }
    }
}
