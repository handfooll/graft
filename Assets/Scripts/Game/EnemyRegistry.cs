using System.Collections.Generic;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Who is alive right now. Kept separate from <see cref="EnemySpawner"/>, which is what *makes*
    /// them, for two reasons that both come due immediately.
    ///
    /// The player's auto-attack needs the list and has no business knowing how spawning works. And
    /// the screenshot tour needs to place a fight by hand, with the spawner switched off, so the
    /// same picture comes out of the same seed - a gate image posed by a live difficulty director
    /// is a different image every run, and a gate you cannot re-shoot is not a gate.
    /// </summary>
    public class EnemyRegistry : MonoBehaviour
    {
        readonly List<EnemyAgent> _live = new List<EnemyAgent>();

        /// <summary>Reused between queries so targeting does not allocate a list every frame.</summary>
        readonly List<Vec2> _positions = new List<Vec2>();

        public IReadOnlyList<EnemyAgent> Live => _live;

        /// <summary>How many are alive. This is what the spawner compares to the population cap.</summary>
        public int Count => _live.Count;

        public void Register(EnemyAgent agent)
        {
            if (agent != null && !_live.Contains(agent))
            {
                _live.Add(agent);
            }
        }

        public void Unregister(EnemyAgent agent) => _live.Remove(agent);

        /// <summary>
        /// The ground-plane positions of everything that can still be shot, in the same order as
        /// <see cref="Live"/>, so an index out of <see cref="Targeting"/> maps straight back.
        ///
        /// Dying enemies are still in <see cref="Live"/> - they spend <c>deathSeconds</c> visibly
        /// collapsing - but they are given a position off the end of the world here rather than
        /// being skipped, because skipping would shift every later index and the auto-attack would
        /// shoot whichever enemy happened to inherit the number.
        /// </summary>
        public IReadOnlyList<Vec2> TargetablePositions()
        {
            _positions.Clear();
            foreach (var agent in _live)
            {
                _positions.Add(agent != null && agent.IsTargetable
                    ? agent.GroundPosition
                    : new Vec2(float.MaxValue * 0.5f, float.MaxValue * 0.5f));
            }

            return _positions;
        }

        public EnemyAgent At(int index) =>
            index >= 0 && index < _live.Count ? _live[index] : null;

        /// <summary>Kills everything on the field outright. Used to reset between gate captures.</summary>
        public void ClearAll()
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] != null)
                {
                    _live[i].Retire();
                }
            }

            _live.Clear();
        }
    }
}
