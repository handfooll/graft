using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Graft.Editor
{
    /// <summary>
    /// Accumulates flat ground tiles into one mesh.
    ///
    /// The world is authored as tiles (BUILD_PLAN.md Phase 2) but must not *render* as thousands of
    /// them: a few thousand quad GameObjects would bloat the scene file into megabytes and hand the
    /// renderer a draw call per tile. Merging them per area gives one mesh, one draw call, and a
    /// scene file that stays readable in a diff - while leaving the tile as the unit the world is
    /// actually designed in.
    /// </summary>
    public sealed class TileMeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<int> _triangles = new List<int>();

        public int TileCount { get; private set; }

        public bool IsEmpty => _triangles.Count == 0;

        /// <summary>
        /// One horizontal tile, centred on <paramref name="centre"/> at height
        /// <paramref name="height"/>.
        ///
        /// Winding is Unity's: the front-face normal of triangle (a, b, c) is
        /// <c>cross(b - a, c - a)</c>. Wound the other way the whole world is invisible from above
        /// and perfectly solid from below, which looks exactly like a failed capture.
        /// </summary>
        public void AddTile(Vector2 centre, float size, float height)
        {
            var half = size * 0.5f;
            var index = _vertices.Count;

            _vertices.Add(new Vector3(centre.x - half, height, centre.y - half));
            _vertices.Add(new Vector3(centre.x - half, height, centre.y + half));
            _vertices.Add(new Vector3(centre.x + half, height, centre.y + half));
            _vertices.Add(new Vector3(centre.x + half, height, centre.y - half));

            for (var i = 0; i < 4; i++)
            {
                _normals.Add(Vector3.up);
            }

            // World-space UVs in tile units, so a future tiling texture lines up across the seams
            // between one area's mesh and the next without anyone re-unwrapping anything.
            _uvs.Add(new Vector2((centre.x - half) / size, (centre.y - half) / size));
            _uvs.Add(new Vector2((centre.x - half) / size, (centre.y + half) / size));
            _uvs.Add(new Vector2((centre.x + half) / size, (centre.y + half) / size));
            _uvs.Add(new Vector2((centre.x + half) / size, (centre.y - half) / size));

            _triangles.Add(index);
            _triangles.Add(index + 1);
            _triangles.Add(index + 2);
            _triangles.Add(index);
            _triangles.Add(index + 2);
            _triangles.Add(index + 3);

            TileCount++;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            // A biome band is thousands of tiles; 16-bit indices top out at 65535 vertices, which
            // is about 16000 tiles, and overflowing it truncates the mesh rather than erroring.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
