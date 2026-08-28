using System.Collections;
using System.IO;
using Graft.Core;
using Graft.Game;
using Graft.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Graft.Tests
{
    /// <summary>
    /// The screenshot gate, as a test. BUILD_PLAN.md: a phase is done when a human has looked at a
    /// PNG and said yes - the previous build passed 50/50 tests while being the wrong game.
    /// Every phase adds its named captures here.
    ///
    /// <code>
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
    ///   -testFilter ScreenshotTour -testResults tour.xml -logFile tour.log
    /// </code>
    ///
    /// No <c>-nographics</c>. With it the captures are blank.
    ///
    /// Phase 0's grey-box capture is gone: the box it was named after is now the player capsule,
    /// and a picture called <c>phase0_grey_box</c> with no grey box in it is worse than no picture.
    /// It was approved on 2026-08-24 and is reproducible from commit <c>37c93bf</c>.
    /// </summary>
    public class ScreenshotTour
    {
        /// <summary>Frames to let URP finish its first-frame setup before capturing.</summary>
        const int SettleFrames = 5;

        /// <summary>
        /// BUILD_PLAN.md Phase 1, image 1 of 3: at rest. Standing still on an off-axis heading, so
        /// the question the gate actually asks - can you tell which way this thing is pointing -
        /// is not accidentally answered by the character happening to align with the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_AtRest()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            player.Teleport(OpenGround(), 205f);
            yield return Seconds(0.5f);

            Assert.Less(player.Speed, 0.01f, "Player has not come to rest.");
            Capture("phase1_at_rest");
        }

        /// <summary>
        /// Image 2 of 3: mid-move. Captured while genuinely at speed, not merely after a nudge -
        /// the assertion is what stops this quietly degrading into a second at-rest picture.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_Moving()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            player.Teleport(OpenGround() + new Vector3(-1.5f, 0f, -5f), 0f);
            yield return Frames(2);

            input.Value = new Vec2(0.55f, 0.83f);
            yield return Seconds(1f);

            Assert.Greater(player.Speed, player.Tuning.maxSpeed * 0.95f,
                "Not actually at speed - this would be an at-rest picture with a different name.");
            Capture("phase1_moving");
        }

        /// <summary>
        /// Image 3 of 3: hard against a wall, still pushing into it. Driven into it rather than
        /// placed there, so what the picture shows is the CharacterController's real behaviour -
        /// sliding along the face instead of sticking to it.
        ///
        /// The wall is now the bank monolith in Base. Phase 1's two scaffolding blocks were
        /// deleted in Phase 2 as STATUS.md said they would be, and the monolith is the nearest
        /// thing the real world has to the flat vertical face this capture is about.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_NearWall()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            // Posed off the monolith's own bounds rather than against copied coordinates: the
            // scene builder lives in an editor-only assembly this test cannot see, and a
            // duplicated constant would go stale silently the first time Base is rearranged.
            var monolith = GameObject.Find("Base_Monolith");
            Assert.IsNotNull(monolith, "Base has no monolith - there is nothing in the world to be against.");
            var wall = monolith.GetComponent<Collider>().bounds;

            player.Teleport(new Vector3(wall.center.x - 0.6f, 0f, wall.min.z - 4.5f), 0f);
            yield return Frames(2);

            // Almost straight into the face, with a slight drift along it. Straight on would show
            // nothing but a stop; too much drift and the player rounds the corner before the
            // shutter opens.
            input.Value = new Vec2(0.14f, 0.99f);
            yield return Seconds(2.5f);

            var position = player.transform.position;
            var gap = wall.min.z - position.z;

            Assert.Greater(gap, 0f, "Walked into the wall - the CharacterController is not colliding.");
            Assert.Less(gap, 1.2f, "Never reached the wall - the capture would show empty ground.");
            Assert.Greater(player.Speed, 0.1f, "Not still pushing into the wall when captured.");
            Capture("phase1_near_wall");
        }

        /// <summary>
        /// Image 4: the floating stick, drawn where a thumb would be. Driven by hand because
        /// <c>-batchmode</c> has no pointer to press - the ring and knob are placed at the same
        /// coordinates the input layer would have produced.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_StickEngaged()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            var view = Object.FindAnyObjectByType<VirtualStickView>();
            Assert.IsNotNull(view, "No VirtualStickView - the HUD is missing from the World scene.");
            var stick = Object.FindAnyObjectByType<VirtualStickInput>();
            Assert.IsNotNull(stick);

            player.Teleport(OpenGround() + new Vector3(0f, 0f, -1.5f), 0f);
            input.Value = new Vec2(0.62f, 0.78f);
            yield return Seconds(1f);

            // Hand control of the view over from the (absent) pointer to this test, and place the
            // ring where a thumb would sit: low and left, pushed up-right, matching the stick above.
            view.Source = null;
            var radius = stick.TravelRadiusPixels;
            var origin = new Vector2(Screen.width * 0.34f, Screen.height * 0.24f);
            view.Show(origin, origin + new Vector2(0.62f, 0.78f) * radius, radius);
            yield return null;

            Capture("phase1_stick");
        }

        // ---------------------------------------------------------------------------------------
        // Phase 2. BUILD_PLAN.md: "one PNG per area, six total. Each must be identifiable as a
        // different place from the picture alone. If two biomes are indistinguishable, the biome
        // design has failed, and no amount of later code fixes it."
        //
        // Numbered in the order you meet them walking out from Base, so a directory listing reads
        // as the journey - the escalation is part of what is being judged, not just the six
        // palettes on their own.
        // ---------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Phase2_Base() => CaptureArea(Area.Base, "phase2_0_base");

        [UnityTest]
        public IEnumerator Phase2_Verge() => CaptureArea(Area.Verge, "phase2_1_verge");

        [UnityTest]
        public IEnumerator Phase2_RustFlats() => CaptureArea(Area.RustFlats, "phase2_2_rust_flats");

        [UnityTest]
        public IEnumerator Phase2_GlassMire() => CaptureArea(Area.GlassMire, "phase2_3_glass_mire");

        [UnityTest]
        public IEnumerator Phase2_BoneReach() => CaptureArea(Area.BoneReach, "phase2_4_bone_reach");

        [UnityTest]
        public IEnumerator Phase2_EmberScar() => CaptureArea(Area.EmberScar, "phase2_5_ember_scar");

        /// <summary>
        /// The image that shows what the cluster layout is *for*: a place where several biomes meet,
        /// so you can see that crossing from one to another is a step rather than an expedition.
        ///
        /// The spot is found by searching for the busiest junction in the world rather than being
        /// typed in, because a hand-picked coordinate stops being a junction the moment anyone
        /// moves a disc - and it would keep taking a perfectly nice picture of one biome while
        /// claiming to prove something about three.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase2_BiomeBorder()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions);

            var junction = BusiestJunction(regions.Map, out var areasInView);
            Assert.GreaterOrEqual(areasInView, 3,
                $"The busiest point in the world has only {areasInView} areas within view of it. " +
                "This layout was chosen so biomes border one another; that has not happened.");

            player.Teleport(junction, regions.Map.OutwardHeading(new Vec2(junction.x, junction.z)).Heading);
            yield return Seconds(0.4f);

            Capture("phase2_7_biome_border");
        }

        /// <summary>
        /// The walkable tile with the most distinct areas inside one screen-width of it - and well
        /// clear of the world's edge.
        ///
        /// The clearance is not cosmetic. Biome discs meet at their rims, and rims are also where
        /// the outer boundary runs, so the first search found a perfectly genuine three-biome
        /// junction with a five-metre wall filling half the frame. A picture of a wall does not
        /// show that crossing between biomes is a step.
        /// </summary>
        static Vector3 BusiestJunction(WorldMap map, out int best)
        {
            // Half the visible width. Anything further away than this is not in the picture, so it
            // cannot be part of what makes the picture show a border.
            var reach = Mathf.CeilToInt(4.75f / map.TileSize);
            var clearance = reach + 2;
            var found = Vector3.zero;
            best = 0;

            for (var row = 0; row < map.Rows; row++)
            {
                for (var column = 0; column < map.Columns; column++)
                {
                    if (!map.IsWalkable(column, row) || !ClearOfTheVoid(map, column, row, clearance))
                    {
                        continue;
                    }

                    var seen = 0;
                    for (var dy = -reach; dy <= reach; dy++)
                    {
                        for (var dx = -reach; dx <= reach; dx++)
                        {
                            var area = map.TileArea(column + dx, row + dy);
                            if (area != Areas.None)
                            {
                                seen |= 1 << area;
                            }
                        }
                    }

                    var count = CountBits(seen);
                    if (count <= best)
                    {
                        continue;
                    }

                    best = count;
                    var centre = map.TileCentre(column, row);
                    found = new Vector3(centre.X, 0f, centre.Y);
                }
            }

            return found;
        }

        static bool ClearOfTheVoid(WorldMap map, int column, int row, int tiles)
        {
            for (var dy = -tiles; dy <= tiles; dy++)
            {
                for (var dx = -tiles; dx <= tiles; dx++)
                {
                    if (!map.IsWalkable(column + dx, row + dy))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        /// <summary>
        /// The seventh image, for Phase 2's other promise: "boundaries so you cannot walk into the
        /// void". The player is driven into the edge of the world at full speed and does not cross
        /// it - which is a claim about collision that a picture of a wall cannot make on its own,
        /// so the assertion carries it and the picture shows what the edge looks like.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase2_WorldEdge()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions);

            // March sideways out of the Verge until the ground stops, then step back off it.
            var anchor = regions.AnchorFor(Area.Verge);
            var outward = Vector3.right;
            var edge = anchor;
            for (var step = 0; step < 400 && regions.TryAreaAt(edge + outward, out _); step++)
            {
                edge += outward;
            }

            Assert.Greater((edge - anchor).magnitude, 5f, "The world ends within 5 m of the Verge anchor.");

            player.Teleport(edge - outward * 6f, 90f);
            yield return Frames(2);

            input.Value = new Vec2(1f, 0f);
            yield return Seconds(3f);

            Assert.IsTrue(regions.TryAreaAt(player.transform.position, out _),
                "Walked straight off the edge of the world - the boundary is not stopping anything.");
            Assert.Greater(player.Speed, 0.1f, "Not still pushing into the boundary when captured.");

            Capture("phase2_6_world_edge");
        }

        // ---------------------------------------------------------------------------------------
        // Phase 3. BUILD_PLAN.md: "a fight in progress - several enemies converging, health bars
        // visible, a hit landing. Plus: enemies must be seen to path around obstacles, not walk
        // through them."
        //
        // Every fight below is posed by hand with the spawn director switched off. A gate image
        // arranged by a live difficulty director is a different image every run, and a gate you
        // cannot re-shoot is not a gate. The one exception is the last capture, which is about what
        // the director itself produces and would be dishonest posed.
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The gate's first sentence: several enemies converging. Four of them, closing from
        /// different bearings, with the player already shooting.
        ///
        /// This is also where STATUS.md's deferred camera question gets its answer - *can I see a
        /// threat in time to react* - because until now there were no threats to see. The
        /// assertion is the useful half: enemies have to be inside the frame and still short of
        /// the player when the shutter opens. If they can only be photographed already touching
        /// them, the framing is too tight and the number in CameraFraming.asset is wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase3_Converging()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            yield return Stage(player, regions.AnchorFor(Area.Verge), 0f);

            // The Verge's own mix - mostly Venom, with an Impact lumbering behind. Bearings are
            // kept shallow because the frame is portrait: at nine metres out, a bearing of 120
            // degrees puts an enemy seven metres to the side, which is a metre and a half outside
            // a 9.5 m frame. It is on the field and in the fight and simply not in the picture.
            Spawn(EnemyFamily.Venom, player, 15f, 9f);
            Spawn(EnemyFamily.Venom, player, -25f, 9.5f);
            Spawn(EnemyFamily.Venom, player, 160f, 8.5f);
            Spawn(EnemyFamily.Impact, player, -150f, 8f);

            // Long enough to be closing, short enough not to have arrived. Venom covers 4.2 m/s,
            // so by 1.6 s they are standing on the player and the picture is of a scrum rather
            // than of a convergence - which is a different thing and a less useful one to judge.
            yield return Seconds(1.2f);

            var registry = Registry();
            var framing = Object.FindAnyObjectByType<CameraRig>().Framing;
            var half = framing.VisibleWidthOnDevice * 0.5f;

            var onScreen = 0;
            var nearest = float.MaxValue;
            foreach (var agent in registry.Live)
            {
                var offset = agent.transform.position - player.transform.position;
                offset.y = 0f;
                nearest = Mathf.Min(nearest, offset.magnitude);
                if (Mathf.Abs(offset.x) <= half)
                {
                    onScreen++;
                }
            }

            Assert.AreEqual(4, onScreen,
                $"Only {onScreen} of the four enemies are within the frame's width. The gate asks " +
                "for several converging, and enemies fighting outside the picture are not that.");
            Assert.Greater(nearest, 2f,
                $"The closest enemy is already {nearest:0.0} m away when the picture is taken, " +
                "which is a scrum rather than a convergence.");

            Capture("phase3_0_converging");
        }

        /// <summary>
        /// The gate's second and third sentences: health bars visible, and a hit landing.
        ///
        /// Timed off the damage itself rather than off a stopwatch. The impact burst lives about
        /// two tenths of a second and the hit flash less than one, so a capture taken at a fixed
        /// delay lands on whichever frame it lands on and the picture is of a fight in which
        /// nothing is happening. Waiting for the blow and then photographing a moment later is the
        /// difference between a gate image and a lucky one.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase3_HitLanding()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            yield return Stage(player, regions.AnchorFor(Area.Verge), 0f);

            // Hide is the target, for two reasons that are both about the picture. It is the
            // slowest family, so it is still at a readable distance after the shots needed to
            // drain its bar rather than standing on top of the player - and it has the most health,
            // so it survives them. Placed just inside the player's reach so the auto-attack opens
            // immediately.
            //
            // A Venom further back keeps its bar at full, because a bar coming down only reads as
            // damage when there is an undamaged one next to it.
            var struck = Spawn(EnemyFamily.Hide, player, 12f, 6.3f);
            Spawn(EnemyFamily.Venom, player, -34f, 9f);
            Assert.IsNotNull(struck, "Nothing could be spawned to shoot at.");

            var hits = 0;
            struck.Health.Damaged += (_, amount) =>
            {
                if (amount > 0f)
                {
                    hits++;
                }
            };

            // Wait for the third blow, so the bar is visibly down rather than a sliver off full.
            var guard = 0f;
            while (hits < 3 && guard < 8f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            Assert.GreaterOrEqual(hits, 3,
                "The player never landed three shots on a target standing inside their reach. " +
                "The auto-attack is not firing.");

            // Long enough for the burst to have opened out, short enough that it is still there.
            yield return Seconds(0.05f);

            Assert.IsTrue(struck.Health.IsAlive, "The target died before the picture was taken.");
            Assert.Less(struck.Health.Fraction, 0.95f, "The target's bar is still effectively full.");

            var range = (struck.transform.position - player.transform.position).magnitude;
            Assert.Greater(range, 2.5f,
                $"The target closed to {range:0.0} m before the third shot landed. At that range " +
                "it, the player and the burst are one overlapping mass and nothing in the picture " +
                "is legible as anything.");

            Capture("phase3_1_hit_landing");
        }

        /// <summary>
        /// The gate's other half, and the reason enemies are NavMeshAgents rather than something
        /// steering at the player: "enemies must be seen to path around obstacles, not walk
        /// through them".
        ///
        /// The obstacle is a real scattered prop, found by searching for the largest collider near
        /// the Verge's anchor rather than by typing a coordinate - props are scattered from a seed
        /// and a hand-picked one stops being there the moment anything about the layout changes.
        ///
        /// A picture cannot prove this on its own: an enemy beside a rock looks the same whether it
        /// went around or is about to go through. So the assertions carry the claim - the path has
        /// a bend in it, and the enemy's own track never entered the obstacle - and the picture
        /// shows what that looks like.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase3_PathingAroundObstacles()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            var obstacle = LargestPropNear(regions.AnchorFor(Area.Verge), 45f);
            Assert.IsNotNull(obstacle,
                "No prop with a collider near the Verge's anchor - there is nothing in the world " +
                "to path around, and the NavMesh would have nothing to carve holes in either.");
            Assert.Greater(Mathf.Min(obstacle.bounds.extents.x, obstacle.bounds.extents.z), 0.7f,
                "The largest prop within 45 m of the Verge is too small to make a detour worth " +
                "photographing. A route bending around a stump is a correct path and an " +
                "unconvincing picture.");

            var bounds = obstacle.bounds;
            var centre = new Vector3(bounds.center.x, 0f, bounds.center.z);
            var girth = Mathf.Max(bounds.extents.x, bounds.extents.z);

            // Player on one side, enemy directly opposite, obstacle exactly between them. Along Z
            // so the detour runs up the screen, which is the long axis in portrait.
            //
            // The gap is what decides whether this is a gate image or just a passing test. Set it
            // too generously and the assertions all still hold - the route bends, the enemy stays
            // out of the rock - while the enemy itself starts beyond the top of the frame and has
            // walked past the obstacle by the time the shutter opens. The picture is then of a
            // player standing next to a boulder, proving nothing.
            var gap = girth + 3.2f;
            yield return Stage(player, centre - Vector3.forward * gap, 0f);

            // Impact rather than Venom: half the speed, so it is still alongside the obstacle when
            // photographed instead of past it, and much the largest silhouette in the game.
            var chaser = Spawn(EnemyFamily.Impact, player, 0f, gap * 2f);
            Assert.IsNotNull(chaser, "Could not place an enemy on the far side of the obstacle.");

            // Let the agent compute a route before asking what it looks like. Waiting on
            // pathPending alone is not enough and fails in a way that looks like a real result:
            // the destination is set from EnemyAgent's own Update, which has not run yet on the
            // frame the spawn returns on, so pathPending is false, the wait falls straight through,
            // and the agent reports a one-corner path - which reads as "walked straight at it".
            var guard = 0f;
            while ((chaser.Agent.pathPending || !chaser.Agent.hasPath) && guard < 3f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(chaser.Agent.hasPath, "The enemy never worked out a route to the player at all.");

            var corners = chaser.Agent.path.corners;
            Assert.GreaterOrEqual(corners.Length, 3,
                $"The route to the player is {corners.Length} corners - a straight line. The " +
                "obstacle is not carving the NavMesh, so nothing is being pathed around.");

            var straight = Vector3.Distance(chaser.transform.position, player.transform.position);
            Assert.Greater(PathLength(corners), straight * 1.05f,
                "The route is no longer than walking straight through the obstacle would be.");

            // Now walk it, watching that the enemy never enters the thing it is going around.
            var flattened = new Bounds(
                new Vector3(bounds.center.x, 0f, bounds.center.z),
                new Vector3(bounds.size.x, 100f, bounds.size.z));

            // Walked until it is alongside the obstacle rather than for a fixed spell: that is the
            // one moment in the whole crossing at which a still image shows a detour rather than
            // just an enemy that happens to be somewhere.
            var deadline = Time.time + 6f;
            while (Time.time < deadline && chaser.transform.position.z > centre.z)
            {
                var at = chaser.transform.position;
                Assert.IsFalse(flattened.Contains(new Vector3(at.x, 0f, at.z)),
                    "The enemy walked into the obstacle rather than around it.");
                yield return null;
            }

            // The assertions above are all true of an enemy that is not in the picture, so this is
            // the one that makes it a gate image: the detour has to be on screen.
            var offset = chaser.transform.position - player.transform.position;
            var framing = Object.FindAnyObjectByType<CameraRig>().Framing;
            Assert.Less(Mathf.Abs(offset.x), framing.VisibleWidthOnDevice * 0.5f,
                $"The enemy is {Mathf.Abs(offset.x):0.0} m to the side and outside the frame - the " +
                "picture would show a player next to a rock and prove nothing.");
            Assert.Less(offset.magnitude, 9f,
                $"The enemy is {offset.magnitude:0.0} m away, past the top of a portrait frame.");
            Assert.IsTrue(chaser.Health.IsAlive, "The enemy was shot before it finished the detour.");

            Capture("phase3_2_pathing_around");
        }

        /// <summary>
        /// The four families side by side, so the thing that actually has to be judged can be:
        /// whether they are tellable apart.
        ///
        /// <c>CombatAssetTests</c> asserts their footprints differ, which is a number. Whether the
        /// difference *reads*, under this camera, on ground that is one of them a similar colour,
        /// is not a number - it is this picture. Left to right in the order DESIGN.md §5 names
        /// them: Venom, Impact, Hide, Spark.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase3_FourFamilies()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            yield return Stage(player, regions.AnchorFor(Area.Verge), 0f);

            // Ranked up-screen of the player and captured immediately, before anyone has taken a
            // step. They are all out of their own reach and would otherwise close and bunch up.
            var origin = player.transform.position + Vector3.forward * 3.4f;
            var spacing = 2.3f;
            var families = EnemyFamilies.All;

            for (var i = 0; i < families.Length; i++)
            {
                var offset = (i - (families.Length - 1) * 0.5f) * spacing;
                var agent = Spawn(families[i], origin + Vector3.right * offset);
                Assert.IsNotNull(agent, $"Could not place a {families[i]} for the family line-up.");
                // Turned to the camera rather than to the player, so all four are seen from the
                // same angle and the comparison is of the shapes rather than of their headings.
                agent.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            yield return Frames(2);

            Assert.AreEqual(families.Length, Registry().TargetableCount,
                "Not every family made it onto the ground for the line-up.");

            Capture("phase3_3_four_families");
        }

        /// <summary>
        /// The one capture that is not posed: Ember Scar, with the real spawn director running.
        ///
        /// Everything above proves a fight can be arranged. This proves the tables produce one -
        /// the deepest biome at its own cap, its own pacing and its own family mix, which is the
        /// difficulty dial of DESIGN.md §7 actually turning rather than being described.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase3_EmberScarSwarm()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            var spawner = Spawner();
            spawner.ClearField();

            player.Teleport(regions.AnchorFor(Area.EmberScar), 0f);
            yield return Frames(2);

            Assert.AreEqual(Area.EmberScar, regions.AreaAt(player.transform.position),
                "Not standing in Ember Scar - the tables being read are the wrong ones.");

            // The director's own pacing, not the tour's. Ember Scar sends one a second to a cap of
            // ten, so a few seconds is a crowd and nothing here decides how big.
            spawner.Running = true;
            yield return Seconds(7f);
            spawner.Running = false;

            var registry = Registry();
            Assert.GreaterOrEqual(registry.Count, 4,
                $"Ember Scar produced only {registry.Count} enemies in seven seconds of its own " +
                "spawning. The deepest biome in the game should be crowded.");
            Assert.AreEqual(Area.EmberScar, spawner.CurrentArea);

            yield return Seconds(1.5f);
            Capture("phase3_4_ember_scar_swarm");
        }

        // -- Phase 3 helpers --------------------------------------------------------------------

        static EnemySpawner Spawner()
        {
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.IsNotNull(spawner, "No EnemySpawner in the World scene - there is no fight to photograph.");
            return spawner;
        }

        static EnemyRegistry Registry()
        {
            var registry = Object.FindAnyObjectByType<EnemyRegistry>();
            Assert.IsNotNull(registry, "No EnemyRegistry in the World scene.");
            return registry;
        }

        /// <summary>Clears the field and puts the player where the fight is going to happen.</summary>
        static IEnumerator Stage(PlayerController player, Vector3 at, float heading)
        {
            var spawner = Spawner();
            spawner.Running = false;
            spawner.ClearField();

            var effects = Object.FindAnyObjectByType<CombatEffects>();
            Assert.IsNotNull(effects, "No CombatEffects in the World scene - nothing could fire.");
            effects.ClearShots();

            player.Teleport(at, heading);
            // Two frames rather than one: the camera is damped, and the first frame after a
            // teleport still has it where the player used to be.
            yield return Frames(2);
        }

        /// <summary>Places one enemy on a bearing and distance from the player.</summary>
        static EnemyAgent Spawn(EnemyFamily family, PlayerController player, float bearing, float distance)
        {
            var offset = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * distance;
            return Spawn(family, player.transform.position + offset);
        }

        static EnemyAgent Spawn(EnemyFamily family, Vector3 at) =>
            Spawner().Spawn(family, at, 1f);

        /// <summary>
        /// The biggest scattered prop with a collider within <paramref name="radius"/>, ignoring
        /// any that sits too near the edge of the world.
        ///
        /// Biggest, because the point is a detour long enough to see - a route bending around a
        /// half-metre stump is a correct path and an unconvincing picture. Ranked on the *smaller*
        /// of its two horizontal extents, because a prop that is wide one way and thin the other
        /// is something an agent walks past rather than around.
        ///
        /// The clearance is the same lesson Phase 2's border capture had to learn. The first
        /// version of this search found a perfectly good boulder that happened to be near the
        /// boundary, and the resulting gate image was a third full of five-metre wall - every
        /// assertion true, and the eye drawn to the one object in frame that has nothing to do
        /// with what is being judged.
        /// </summary>
        static Collider LargestPropNear(Vector3 centre, float radius)
        {
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Collider best = null;
            var bestSize = 0f;

            foreach (var hit in Physics.OverlapSphere(centre, radius))
            {
                var owner = hit.transform.parent;
                if (owner == null || !owner.name.EndsWith("_Prop"))
                {
                    continue;
                }

                var size = Mathf.Min(hit.bounds.extents.x, hit.bounds.extents.z);
                if (size <= bestSize || !ClearOfTheEdge(regions, hit.bounds.center, 14f))
                {
                    continue;
                }

                bestSize = size;
                best = hit;
            }

            return best;
        }

        /// <summary>Whether the world still exists <paramref name="reach"/> metres out all round.</summary>
        static bool ClearOfTheEdge(WorldRegions regions, Vector3 at, float reach)
        {
            if (regions == null)
            {
                return true;
            }

            for (var bearing = 0f; bearing < 360f; bearing += 45f)
            {
                var probe = at + Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * reach;
                if (!regions.TryAreaAt(probe, out _))
                {
                    return false;
                }
            }

            return true;
        }

        static float PathLength(Vector3[] corners)
        {
            var total = 0f;
            for (var i = 1; i < corners.Length; i++)
            {
                total += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return total;
        }

        /// <summary>
        /// Stands the player at an area's anchor, facing the way you would be walking, and
        /// captures.
        ///
        /// The two assertions are the ones a picture cannot make for itself. That the player is
        /// really in the area the file is named after - a gate of six images is worthless if two
        /// of them are the same place under different names. And that the NavMesh reaches here -
        /// an unbaked patch looks exactly like a baked one until Phase 3 puts an enemy on it.
        /// </summary>
        static IEnumerator CaptureArea(Area area, string name)
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions, "No WorldRegions in the World scene - there is no world to photograph.");

            var anchor = regions.AnchorFor(area);
            player.Teleport(anchor, HeadingAt(regions, area, anchor));
            yield return Seconds(0.4f);

            Assert.AreEqual(area, regions.AreaAt(player.transform.position),
                $"Standing at {area}'s anchor puts the player in " +
                $"{regions.AreaAt(player.transform.position)} - the gate would photograph the wrong place.");
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out _, 2f, NavMesh.AllAreas),
                $"No NavMesh within 2 m of {area}'s anchor - enemies could never reach this biome.");

            Capture(name);
        }

        /// <summary>
        /// Facing the way the world goes. Base faces its own monument; a biome faces further out,
        /// which is the direction the player is looking when they choose to keep going.
        ///
        /// Asked of the map rather than worked out here, because "further out" means different
        /// things in the two layouts - along the spine in a corridor, away from Base in a cluster.
        /// </summary>
        static float HeadingAt(WorldRegions regions, Area area, Vector3 anchor) =>
            area == Area.Base ? 0f : regions.Map.OutwardHeading(new Vec2(anchor.x, anchor.z)).Heading;

        /// <summary>
        /// Where the Phase 1 captures are posed, now that the world exists: the clear ground at the
        /// Verge's anchor, rather than the origin.
        ///
        /// The origin is inside the Base plaza these days, and those three images ask one question -
        /// can you tell which way the character is facing - which a monument and a ring of pillars
        /// crowding the frame does not help anyone answer.
        /// </summary>
        static Vector3 OpenGround()
        {
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions, "No WorldRegions in the World scene.");
            return regions.AnchorFor(Area.Verge);
        }

        /// <summary>
        /// Renders the capture, then holds it against the version a human approved.
        ///
        /// The baseline check is the part that earns its place. Every assertion in this file
        /// describes something a test author thought to look for; the baseline catches what nobody
        /// thought of - an enemy that has wandered into a picture about biome colour, a health bar
        /// that stopped rendering, a shadow that vanished when the sun angle moved. All of those
        /// leave every other test green.
        ///
        /// Set <c>GRAFT_REBASELINE=1</c> to write the captures in as the new approved images
        /// instead of comparing against them. That is a deliberate, separate act - a gate that
        /// re-approves whatever it just rendered is not a gate.
        /// </summary>
        static void Capture(string name)
        {
            var path = Screenshot.Capture(name);
            Assert.IsTrue(File.Exists(path), "Capture wrote no file.");
            Assert.Greater(new FileInfo(path).Length, 2048,
                "Capture is suspiciously small - this is what an empty render looks like. " +
                "Check the tour was run without -nographics.");

            if (Rebaselining)
            {
                GateBaselines.Approve(name, path);
                Debug.Log($"[GRAFT] baseline approved -> {name}");
                return;
            }

            var changed = GateBaselines.CompareWithBaseline(name, path, out var detail);
            if (changed < 0f)
            {
                // No baseline yet. A warning rather than a failure, so adding a capture does not
                // break the tour on the run that introduces it.
                Debug.LogWarning($"[GRAFT] {detail}");
                return;
            }

            Assert.LessOrEqual(changed, GateBaselines.ChangedPixelTolerance,
                $"'{name}' no longer matches the image that was approved. {detail}\n" +
                "If the change is intended, look at it first and then re-approve with:\n" +
                "  GRAFT_REBASELINE=1 <tour command>\n" +
                "If it is not intended, something has changed this picture without meaning to - " +
                "which is the entire reason this check exists.");
        }

        static bool Rebaselining =>
            System.Environment.GetEnvironmentVariable("GRAFT_REBASELINE") == "1";

        static PlayerController FindPlayer()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "World scene has no PlayerController.");
            Assert.IsNotNull(player.Tuning,
                "PlayerController has no MovementTuning - it would stand still and report no error.");
            return player;
        }

        static ScriptedMoveInput Drive(PlayerController player, Vec2 initial)
        {
            var input = new ScriptedMoveInput { Value = initial };
            player.Input = input;
            return input;
        }

        static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Drives for a span of simulated time rather than a count of frames. A frame count buys
        /// an unpredictable amount of movement - a capture posed by frame count would drift the
        /// moment this machine got faster.
        ///
        /// Simulated time is only trustworthy here because <see cref="CaptureTimestep"/> pins the
        /// clock to a fixed step per frame. Read that note before changing either.
        /// </summary>
        static IEnumerator Seconds(float seconds)
        {
            var deadline = Time.time + seconds;
            var guard = 0;
            while (Time.time < deadline && guard++ < 100000)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Simulated seconds per frame while the tour runs.
        ///
        /// **Without this the gate images are not reproducible, and that is not a small thing** -
        /// the whole reason `Screenshots/` is gitignored is the claim that every image in it can be
        /// re-made from the tests. Headless play mode runs uncapped, so a pose held for "1.2
        /// seconds" gets a different number of frames on every run and a different number of
        /// NavMesh steps with it. Two Phase 3 captures drifted by 8.2% and 2.4% of their pixels
        /// between consecutive runs of identical code.
        ///
        /// <c>Time.captureDeltaTime</c> is Unity's answer to exactly this: every frame advances the
        /// clock by precisely this much, whatever the machine was doing. It is what recording tools
        /// use, and a gate image is a recording.
        /// </summary>
        const float CaptureTimestep = 1f / 60f;

        /// <summary>
        /// Fixed so that anything asking for a random number during a capture asks for the same
        /// one every time. `EnemyAgent` spreads NavMesh avoidance priorities randomly, which is
        /// right in play - a crowd converging on one point should not deadlock with every agent
        /// equally entitled to the same metre - and is one of the things that made these two
        /// pictures different on every run.
        /// </summary>
        const int CaptureSeed = 20260828;

        static IEnumerator LoadWorld()
        {
            Screenshot.UseDeviceQualityLevel();
            Time.captureDeltaTime = CaptureTimestep;
            Random.InitState(CaptureSeed);
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);

            // Spawning off by default, for every capture in this file.
            //
            // Phase 3 put a live spawn director in the World scene, and the Phase 1 and Phase 2
            // gate images were approved without one. Left running, it would quietly redecorate
            // eleven already-signed-off pictures with enemies - and "the world takes shape" is not
            // a claim you can re-judge through a fight happening in front of it. The Phase 3
            // captures turn it back on, or pose their own.
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.Running = false;
            }

            yield return Frames(SettleFrames);
            Assert.IsNotNull(Camera.main, "World scene has no camera tagged MainCamera.");
        }
    }
}
