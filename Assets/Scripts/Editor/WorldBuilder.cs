using System.Collections.Generic;
using System.IO;
using Graft.Core;
using Graft.Game;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Graft.Editor
{
    /// <summary>
    /// Turns a <see cref="WorldLayout"/> into the geometry of the world: ground, boundary, the
    /// Base plaza, scatter props, and a baked NavMesh over all of it.
    ///
    /// It decides nothing. Every question about *where* things are was already answered by
    /// <see cref="WorldMap"/> in <c>Graft.Core</c>, where it is testable without an engine; this
    /// file only turns those answers into meshes. If a biome is in the wrong place, the bug is in
    /// Core and a test can catch it - which is the arrangement BUILD_PLAN.md rule 4 is for.
    /// </summary>
    public static class WorldBuilder
    {
        public const string RootName = "World";
        public const string MeshFolder = "Assets/Meshes/World";
        public const string NavMeshAssetPath = "Assets/Scenes/World_NavMesh.asset";

        /// <summary>Height of the dead ground outside the wall - below the world, so it reads as a drop.</summary>
        const float VoidHeight = -0.7f;

        /// <summary>
        /// Metres kept clear around each area's anchor. The anchors are where the gate captures
        /// stand and where Phase 3 will spawn, and a prop scattered onto one puts the player inside
        /// a rock. Three metres out of a 350 m world is not staging the picture; it is not spawning
        /// inside the scenery.
        /// </summary>
        const float AnchorClearance = 3f;

        public static GameObject Build(WorldLayout layout)
        {
            var map = layout.BuildMap();
            ResetMeshFolder();

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);

            BuildGround(root.transform, layout, map);
            BuildVoidSkirt(root.transform, layout, map);
            BuildBoundary(root.transform, layout, map);
            var baseCentre = map.AnchorFor(Area.Base);
            BaseBuilder.Build(root.transform, layout, new Vector3(baseCentre.X, 0f, baseCentre.Y));
            ScatterProps(root.transform, layout, map);

            var regions = root.AddComponent<WorldRegions>();
            regions.Layout = layout;

            BakeNavMesh(root);

            Debug.Log($"[GRAFT] {map.Shape} world built: {map.CountWalkable()} tiles over " +
                      $"{map.CountWalkable() * map.TileSize * map.TileSize:N0} m². " +
                      Reach(map) + " " + Borders(map));
            return root;
        }

        /// <summary>
        /// How deep the world goes, in the terms its own shape is measured in. A corridor has a
        /// spine length; a cluster does not have a spine at all, and asking one for it is a null
        /// reference in a log line that was only ever meant to be reassuring.
        /// </summary>
        static string Reach(WorldMap map)
        {
            var deepest = map.DistanceFromBase(map.AnchorFor(Area.EmberScar));
            return map.Shape == WorldShape.Corridor
                ? $"Spine {map.Path.Length:0} m; Ember Scar {deepest:0} m out."
                : $"Ember Scar {deepest:0} m from Base.";
        }

        /// <summary>
        /// Who borders whom, measured off the finished tiles. Logged because it is the property the
        /// cluster layout was chosen for, and because it is decided by disc overlaps interacting
        /// with the wobble - which is to say, not by anything you can read off the numbers.
        /// </summary>
        static string Borders(WorldMap map)
        {
            var summary = new System.Text.StringBuilder("Borders:");
            foreach (var area in Areas.Biomes)
            {
                var neighbours = new List<string>();
                foreach (var other in map.NeighboursOf(area))
                {
                    neighbours.Add(other.ToString());
                }

                summary.Append($" {area}={neighbours.Count}({string.Join("/", neighbours)})");
            }

            return summary.ToString();
        }

        static void ResetMeshFolder()
        {
            if (AssetDatabase.IsValidFolder(MeshFolder))
            {
                AssetDatabase.DeleteAsset(MeshFolder);
            }

            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Two meshes per area - the dominant colour and a scattered second shade. One flat fill
        /// under this camera has no grain and no scale, so a gate image of it is an image of
        /// nothing; the mottle is what makes the ground look like ground.
        /// </summary>
        static void BuildGround(Transform parent, WorldLayout layout, WorldMap map)
        {
            var ground = new GameObject("Ground");
            ground.transform.SetParent(parent, false);

            foreach (var area in Areas.All)
            {
                var palette = layout.PaletteFor(area);
                var main = new TileMeshBuilder();
                var mottle = new TileMeshBuilder();

                for (var row = 0; row < map.Rows; row++)
                {
                    for (var column = 0; column < map.Columns; column++)
                    {
                        if (map.TileArea(column, row) != (int)area)
                        {
                            continue;
                        }

                        for (var subRow = 0; subRow < WorldMap.MottlePerTile; subRow++)
                        {
                            for (var subColumn = 0; subColumn < WorldMap.MottlePerTile; subColumn++)
                            {
                                var centre = map.MottleCentre(column, row, subColumn, subRow);
                                var target = map.IsMottled(column, row, subColumn, subRow) ? mottle : main;
                                target.AddTile(new Vector2(centre.X, centre.Y), map.MottleSize, 0f);
                            }
                        }
                    }
                }

                Emit(ground.transform, $"Ground_{area}", main,
                    MaterialLibrary.GetOrCreate($"M_Ground_{area}", palette.ground), collide: true);
                Emit(ground.transform, $"Ground_{area}_Mottle", mottle,
                    MaterialLibrary.GetOrCreate($"M_Ground_{area}_Mottle", palette.groundMottle), collide: true);
            }
        }

        /// <summary>
        /// Dead ground outside the wall. Purely so the camera, which looks outward and downward,
        /// never frames the clear colour - an unfilled corner of the screen reads as a rendering
        /// fault, not as the edge of the map. No collider: the wall is what stops you.
        /// </summary>
        static void BuildVoidSkirt(Transform parent, WorldLayout layout, WorldMap map)
        {
            var builder = new TileMeshBuilder();
            for (var row = 0; row < map.Rows; row++)
            {
                for (var column = 0; column < map.Columns; column++)
                {
                    if (!map.IsSkirt(column, row))
                    {
                        continue;
                    }

                    var centre = map.TileCentre(column, row);
                    builder.AddTile(new Vector2(centre.X, centre.Y), map.TileSize, VoidHeight);
                }
            }

            Emit(parent, "Void", builder,
                MaterialLibrary.GetOrCreate("M_Void", layout.voidColour), collide: false);
        }

        static void Emit(Transform parent, string name, TileMeshBuilder builder, Material material, bool collide)
        {
            if (builder.IsEmpty)
            {
                return;
            }

            var mesh = builder.ToMesh(name);
            AssetDatabase.CreateAsset(mesh, $"{MeshFolder}/{name}.asset");

            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.isStatic = true;
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Flat ground has nothing to cast onto, and including it as a caster only buys
            // depth-bias artefacts - a faint diagonal band that later reads as a fake biome edge.
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            if (collide)
            {
                piece.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        /// <summary>
        /// The wall around the world, built from exactly the tile edges where the ground stops
        /// (BUILD_PLAN.md Phase 2: "boundaries so you cannot walk into the void") - so it can never
        /// disagree with the ground it encloses.
        ///
        /// Runs of edges are merged into single blocks. A block per tile edge would be some
        /// thousands of colliders around a 350 m world; merged, it is a couple of hundred.
        /// </summary>
        static void BuildBoundary(Transform parent, WorldLayout layout, WorldMap map)
        {
            var boundary = new GameObject("Boundary");
            boundary.transform.SetParent(parent, false);
            var material = MaterialLibrary.GetOrCreate("M_Boundary", layout.boundaryColour);

            var edges = new HashSet<(int column, int row, int dx, int dy)>();
            foreach (var edge in map.BoundaryEdges())
            {
                edges.Add((edge.Column, edge.Row, edge.DirectionX, edge.DirectionY));
            }

            var placed = new HashSet<(int, int, int, int)>();
            var tile = map.TileSize;
            var height = layout.boundaryHeight;
            var thickness = layout.boundaryThickness;

            foreach (var edge in edges)
            {
                if (placed.Contains(edge))
                {
                    continue;
                }

                // Walk the run in whichever direction the face is flat along. The set is unordered,
                // so rewind to the run's start first - beginning in the middle of one would leave
                // its first half behind as a gap in the wall.
                var stepColumn = edge.dx != 0 ? 0 : 1;
                var stepRow = edge.dx != 0 ? 1 : 0;
                var startColumn = edge.column;
                var startRow = edge.row;
                while (edges.Contains((startColumn - stepColumn, startRow - stepRow, edge.dx, edge.dy)))
                {
                    startColumn -= stepColumn;
                    startRow -= stepRow;
                }

                var length = 0;
                var column = startColumn;
                var row = startRow;
                while (edges.Contains((column, row, edge.dx, edge.dy)))
                {
                    placed.Add((column, row, edge.dx, edge.dy));
                    column += stepColumn;
                    row += stepRow;
                    length++;
                }

                var firstCentre = map.TileCentre(startColumn, startRow);
                var lastCentre = map.TileCentre(startColumn + stepColumn * (length - 1), startRow + stepRow * (length - 1));
                var mid = new Vector3((firstCentre.X + lastCentre.X) * 0.5f, 0f, (firstCentre.Y + lastCentre.Y) * 0.5f);
                var face = new Vector3(edge.dx, 0f, edge.dy) * (tile * 0.5f);
                var span = length * tile;

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(boundary.transform, false);
                // Sunk slightly, so no sliver of dead ground shows under it on a sloped view.
                wall.transform.position = mid + face + new Vector3(0f, height * 0.5f - 0.3f, 0f);
                wall.transform.localScale = edge.dx != 0
                    ? new Vector3(thickness, height, span)
                    : new Vector3(span, height, thickness);
                var renderer = wall.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // The wall never casts. At 5.5 m tall under a 38-degree sun it throws seven metres
                // of shadow back across the ground the player is standing on, which turns the whole
                // rim of the world into a black trench and hides the biome it is supposed to edge.
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                wall.isStatic = true;
            }

            Debug.Log($"[GRAFT] Boundary: {boundary.transform.childCount} blocks over {edges.Count} tile edges.");
        }

        /// <summary>
        /// Deterministic scatter, seeded per area, with a minimum spacing enforced by rejection.
        /// Deterministic because the world has to be the same world in every gate image and on the
        /// phone - a scatter that reshuffled per build would make two screenshots of the same place
        /// impossible to compare.
        /// </summary>
        static void ScatterProps(Transform parent, WorldLayout layout, WorldMap map)
        {
            var props = new GameObject("Props");
            props.transform.SetParent(parent, false);

            var anchors = new List<Vector2>();
            foreach (var area in Areas.All)
            {
                var anchor = map.AnchorFor(area);
                anchors.Add(new Vector2(anchor.X, anchor.Y));
            }

            var total = 0;
            foreach (var area in Areas.Biomes)
            {
                var palette = layout.PaletteFor(area);
                if (palette.propsPer100SqMetres <= 0f)
                {
                    continue;
                }

                var tiles = new List<(int column, int row)>();
                for (var row = 0; row < map.Rows; row++)
                {
                    for (var column = 0; column < map.Columns; column++)
                    {
                        if (map.TileArea(column, row) == (int)area)
                        {
                            tiles.Add((column, row));
                        }
                    }
                }

                var areaSquareMetres = tiles.Count * map.TileSize * map.TileSize;
                var target = Mathf.RoundToInt(areaSquareMetres * palette.propsPer100SqMetres / 100f);
                if (target <= 0 || tiles.Count == 0)
                {
                    continue;
                }

                var group = new GameObject(palette.displayName);
                group.transform.SetParent(props.transform, false);

                var rng = new System.Random(layout.seed + (int)area * 7919);
                var body = MaterialLibrary.GetOrCreate($"M_Prop_{area}", palette.prop);
                var accent = MaterialLibrary.GetOrCreate($"M_PropAccent_{area}", palette.propAccent, palette.accentEmissive);
                var shade = MaterialLibrary.GetOrCreate($"M_PropShade_{area}", palette.propShade);

                var taken = new HashSet<(int, int)>();
                var cell = Mathf.Max(0.5f, palette.propSpacing);
                var placed = 0;
                for (var attempt = 0; attempt < target * 12 && placed < target; attempt++)
                {
                    var (column, row) = tiles[rng.Next(tiles.Count)];
                    var centre = map.TileCentre(column, row);
                    var jitter = map.TileSize * 0.5f;
                    var point = new Vector2(
                        centre.X + (float)(rng.NextDouble() - 0.5) * jitter,
                        centre.Y + (float)(rng.NextDouble() - 0.5) * jitter);

                    var key = (Mathf.FloorToInt(point.x / cell), Mathf.FloorToInt(point.y / cell));
                    if (!taken.Add(key))
                    {
                        continue;
                    }

                    if (TooCloseToAnAnchor(point, anchors))
                    {
                        continue;
                    }

                    AreaProps.Create(area, group.transform, new Vector3(point.x, 0f, point.y), rng, body, accent, shade);
                    placed++;
                }

                total += placed;
            }

            Debug.Log($"[GRAFT] Scattered {total} props.");
        }

        static bool TooCloseToAnAnchor(Vector2 point, List<Vector2> anchors)
        {
            foreach (var anchor in anchors)
            {
                if ((point - anchor).sqrMagnitude < AnchorClearance * AnchorClearance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One NavMeshSurface over the whole world (REFERENCE_XPHERO.md: the shipped game bakes
        /// NavMesh with this same package). Built from physics colliders rather than render meshes,
        /// so the flat decorations that deliberately have no collider - plates, pools, paving - do
        /// not punch holes in a surface the player walks straight over.
        ///
        /// The data is written out as an asset. Left in memory it would be rebuilt on every load,
        /// which on a world this size is a visible stall, and would not exist at all in a player build.
        /// </summary>
        static void BakeNavMesh(GameObject root)
        {
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;

            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[GRAFT] NavMesh bake produced no data - enemies would have nothing to walk on.");
                return;
            }

            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            Debug.Log("[GRAFT] NavMesh baked and saved to " + NavMeshAssetPath);
        }
    }
}
