using System.Collections.Generic;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Every mote of essence lying in the world: what makes them, what moves them, and what puts
    /// them away again.
    ///
    /// Pooled and ticked centrally, exactly as <see cref="CombatEffects"/> pools shots and bursts,
    /// and for a sharper version of the same reason. A full expedition is two hundred motes; a
    /// deep kill drops eleven of them at once; and at full cargo they *accumulate* on the ground
    /// rather than being consumed. Instantiating that is a stutter on the phone (D8).
    ///
    /// Drops hang off <see cref="Damageable.Died"/>, which already fires exactly once and is
    /// already guarded against a second shot arriving mid-collapse and killing the same thing
    /// twice. There is deliberately no second death path - the spec says so, and the guard is the
    /// reason.
    /// </summary>
    public class EssenceField : MonoBehaviour
    {
        [SerializeField] EssenceTuning tuning;
        [SerializeField] WorldRegions regions;
        [SerializeField] EssenceMagnet magnet;

        readonly Stack<EssenceDrop> _idle = new Stack<EssenceDrop>();
        readonly List<EssenceDrop> _live = new List<EssenceDrop>();

        Transform _pool;
        bool _wasFull;

        public EssenceTuning Tuning { get => tuning; set => tuning = value; }
        public WorldRegions Regions { get => regions; set => regions = value; }
        public EssenceMagnet Magnet { get => magnet; set => magnet = value; }

        /// <summary>Motes on the field right now - resting or in flight. Read by the tour.</summary>
        public int LiveCount => _live.Count;

        /// <summary>Motes lying unclaimed. At full cargo this is the number that climbs.</summary>
        public int RestingCount
        {
            get
            {
                var count = 0;
                foreach (var drop in _live)
                {
                    if (drop != null && drop.IsResting)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Motes in the air on their way to the player. The gate photographs this: a mote at rest
        /// and a mote being drawn in are the same object in a still image, so the only thing that
        /// makes absorption legible is catching several of them strung out mid-flight.
        /// </summary>
        public int FlyingCount => _live.Count - RestingCount;

        void Awake()
        {
            _pool = new GameObject("Essence").transform;
            _pool.SetParent(transform, false);
        }

        /// <summary>
        /// Spills the essence one death is worth.
        ///
        /// <paramref name="effectiveMaxHealth"/> is the enemy's health *after* depth scaling - read
        /// straight off its <see cref="Damageable"/>, so the drop cannot disagree with what the
        /// player actually had to shoot through. A mote is worth what it cost you; see
        /// <see cref="EssenceRules"/> for why that, and not a flat amount per kill.
        /// </summary>
        public int Drop(EnemyFamily family, Vector3 at, float effectiveMaxHealth)
        {
            if (tuning == null)
            {
                return 0;
            }

            var rules = tuning.Rules;
            var count = rules.MotesFor(effectiveMaxHealth);
            var origin = regions != null ? regions.AreaAt(at) : Area.Verge;
            var mote = new EssenceMote(family, tuning.cargoPerMote, origin);

            for (var i = 0; i < count; i++)
            {
                var drop = _idle.Count > 0 ? _idle.Pop() : NewDrop();
                drop.Place(mote, Scatter(at, i, count), tuning.moteSize, tuning.moteLifetime,
                    MaterialFor());
                _live.Add(drop);
            }

            return count;
        }

        /// <summary>
        /// Spreads a shower around the kill so it reads as several things rather than one bright
        /// point. Deterministic in the index rather than random: the gate photographs a mid-fight
        /// moment, and a capture whose motes land somewhere different every run is a baseline that
        /// fails for no reason.
        /// </summary>
        Vector3 Scatter(Vector3 at, int index, int count)
        {
            if (count <= 1 || tuning.scatterRadius <= 0f)
            {
                return at;
            }

            // Golden-angle spiral: consecutive motes never land beside each other, and the shower
            // fills its disc evenly at any count.
            const float GoldenAngle = 137.508f;
            var radius = tuning.scatterRadius * Mathf.Sqrt((index + 0.5f) / count);
            var bearing = index * GoldenAngle * Mathf.Deg2Rad;
            return at + new Vector3(Mathf.Cos(bearing) * radius, 0f, Mathf.Sin(bearing) * radius);
        }

        Material MaterialFor()
        {
            // Unclaimable motes are drawn differently, because a full bar is a thing the player has
            // to be able to read off the ground rather than off the HUD.
            var full = magnet != null && magnet.Ledger != null && magnet.Ledger.IsFull;
            var wanted = full ? tuning.moteFullMaterial : tuning.moteMaterial;
            return wanted != null ? wanted : tuning.moteMaterial;
        }

        void Update()
        {
            if (tuning == null)
            {
                return;
            }

            RestyleOnFullChange();

            if (_live.Count == 0)
            {
                return;
            }

            var player = magnet != null ? magnet.transform.position : Vector3.zero;
            var deltaTime = Time.deltaTime;

            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var drop = _live[i];
                if (drop == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                if (drop.Expired)
                {
                    Retire(i, drop);
                    continue;
                }

                if (!drop.IsFlying && magnet != null && magnet.InRange(drop.transform.position)
                    && magnet.WillAttract(drop.Mote))
                {
                    drop.BeginFlight();
                }

                if (!drop.Advance(player, magnet != null ? magnet.FlightSpeed : 9f, deltaTime))
                {
                    continue;
                }

                // Arrived. The absorb can still fail - the bar may have filled during the flight -
                // in which case the mote settles where it is rather than vanishing into a hold that
                // had no room for it.
                if (magnet != null && magnet.Absorb(drop.Mote))
                {
                    Retire(i, drop);
                }
                else
                {
                    drop.Settle();
                }
            }
        }

        /// <summary>
        /// Restyles every mote on the ground when the cargo bar fills or empties.
        ///
        /// On the edge only, never per frame. This is the half of DESIGN.md §4's "non-nagging
        /// prompt" that lives in the world rather than in the HUD: at capacity the essence you are
        /// still killing for lies there in a colour that says it is not coming, which is a thing
        /// you notice while looking at the fight instead of a banner that interrupts it. It is also
        /// what makes the phase's third gate image legible as a full bar rather than as a busy one.
        /// </summary>
        void RestyleOnFullChange()
        {
            var full = magnet != null && magnet.Ledger != null && magnet.Ledger.IsFull;
            if (full == _wasFull)
            {
                return;
            }

            _wasFull = full;
            var material = MaterialFor();
            foreach (var drop in _live)
            {
                if (drop != null)
                {
                    drop.SetMaterial(material);
                }
            }
        }

        void Retire(int index, EssenceDrop drop)
        {
            drop.Retire();
            _live.RemoveAt(index);
            _idle.Push(drop);
        }

        /// <summary>
        /// Sweeps the field. The screenshot tour poses one scene for every capture without
        /// reloading between them, and motes left over from the previous pose would drift into the
        /// next picture - the same reason <c>EnemySpawner.ClearField</c> exists.
        /// </summary>
        public void Clear()
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                Retire(i, _live[i]);
            }
        }

        EssenceDrop NewDrop()
        {
            var drop = new GameObject("Essence").AddComponent<EssenceDrop>();
            drop.transform.SetParent(_pool, false);
            drop.gameObject.SetActive(false);
            return drop;
        }
    }
}
