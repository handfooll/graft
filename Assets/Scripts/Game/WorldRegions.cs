using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Answers "where in the world am I" at runtime, from the same <see cref="WorldMap"/> the
    /// editor built the ground out of. Sharing the map is the point: an area lookup that disagreed
    /// with the colour of the floor under your feet would be invisible until something important
    /// depended on it - Phase 3's spawn tables, Phase 4's banking, Phase 4's way home.
    ///
    /// The map is regenerated on Awake rather than serialised. It is a few thousand tile
    /// classifications and costs well under a frame, and it means a change to the layout asset can
    /// never leave a stale copy baked into the scene.
    /// </summary>
    public class WorldRegions : MonoBehaviour
    {
        [SerializeField] WorldLayout layout;

        WorldMap _map;

        public WorldLayout Layout
        {
            get => layout;
            set
            {
                layout = value;
                _map = null;
            }
        }

        public WorldMap Map => _map ??= layout != null ? layout.BuildMap() : null;

        void Awake()
        {
            // Touch it now so the cost lands on load rather than on the first query mid-fight.
            _ = Map;
        }

        /// <summary>
        /// The area at a world position. Points outside the world answer with the nearest thing
        /// that makes sense - <see cref="Area.Base"/> - because "outside the world" is not a place
        /// the boundary wall lets the player reach, and returning a nullable everywhere would put a
        /// null check in front of every caller for a case that cannot happen.
        /// </summary>
        public Area AreaAt(Vector3 position)
        {
            return TryAreaAt(position, out var area) ? area : Area.Base;
        }

        public bool TryAreaAt(Vector3 position, out Area area)
        {
            area = Area.Base;
            if (Map == null)
            {
                return false;
            }

            var tile = Map.AreaAt(new Vec2(position.x, position.z));
            if (tile == Areas.None)
            {
                return false;
            }

            area = (Area)tile;
            return true;
        }

        /// <summary>Where banking happens and where death respawns you (§3). Phase 4 uses this.</summary>
        public bool IsInBase(Vector3 position) =>
            TryAreaAt(position, out var area) && area == Area.Base;

        /// <summary>Metres back to Base along the spine - the cost of the walk home.</summary>
        public float DistanceFromBase(Vector3 position) =>
            Map != null ? Map.DistanceFromBase(new Vec2(position.x, position.z)) : 0f;

        /// <summary>A representative standing spot in an area, on the ground plane.</summary>
        public Vector3 AnchorFor(Area area)
        {
            if (Map == null)
            {
                return Vector3.zero;
            }

            var point = Map.AnchorFor(area);
            return new Vector3(point.X, 0f, point.Y);
        }
    }
}
