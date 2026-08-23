using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Graft.Core;
using Graft.Game;
using Graft.UI;

namespace Graft.App
{
    /// <summary>
    /// The app root. Builds the entire game programmatically at startup -- camera, arena,
    /// canvases, screens -- so there is no hand-authored scene that can be subtly wrong and
    /// a fresh clone of the repo runs correctly with an empty scene.
    /// Also implements <see cref="RunContext"/>, which is the only thing combat can see.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour, RunContext
    {
        public static GameFlow Instance { get; private set; }

        public RunState Run { get; private set; }

        PlayerProfile _profile;
        public PlayerProfile Profile => _profile;
        SaveService _saves;

        Arena _arena;
        Transform _enemyParent;
        Transform _fxParent;
        Camera _camera;

        PlayerController _player;
        PlayerAvatar _avatar;
        public PlayerAvatar Avatar => _avatar;
        AbilityRunner _abilities;
        WaveDirector _director;
        public WaveDirector Director => _director;

        Canvas _hudCanvas;
        Canvas _overlayCanvas;
        CombatHUD _hud;
        GraftChoiceCard _graftCard;
        RunEndScreen _endScreen;
        MetaScreen _metaScreen;
        VirtualStick _stick;

        bool _runActive;
        public bool IsRunActive => _runActive;
        bool _resolvingEnd;
        readonly Queue<EnemyArchetype> _pendingOffers = new Queue<EnemyArchetype>();

        static readonly Vector2 ArenaBounds = new Vector2(7.4f, 13.2f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameFlow");
            DontDestroyOnLoad(go);
            go.AddComponent<GameFlow>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _saves = new SaveService(ResolveSaveDirectory());
            _profile = _saves.Load();

            BuildWorld();
            BuildUI();

            _metaScreen.Show(_profile, StartRun, SaveProfile);
        }

        /// <summary>
        /// Batch mode is only ever a test run, a CI run, or a dev tool -- never a real
        /// player. Redirecting the save directory there means none of those can quietly
        /// overwrite someone's real profile, the way a shared path eventually would.
        /// </summary>
        static string ResolveSaveDirectory()
        {
            return Application.isBatchMode
                ? System.IO.Path.Combine(Application.persistentDataPath, "batchmode_testdata")
                : Application.persistentDataPath;
        }

        void BuildWorld()
        {
            var camGo = new GameObject("MainCamera", typeof(Camera));
            camGo.transform.SetParent(transform, false);
            _camera = camGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.Floor;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.tag = "MainCamera";

            var arenaGo = new GameObject("Arena");
            arenaGo.transform.SetParent(transform, false);
            _arena = arenaGo.AddComponent<Arena>();
            _arena.Bounds = ArenaBounds;

            _enemyParent = new GameObject("Enemies").transform;
            _enemyParent.SetParent(arenaGo.transform, false);
            _fxParent = new GameObject("FX").transform;
            _fxParent.SetParent(arenaGo.transform, false);

            BuildFloor(arenaGo.transform);
            FitCamera();
        }

        /// <summary>A grid floor so movement reads as movement -- without it a bounded arena
        /// with a static camera looks static even when the hero is sprinting.</summary>
        void BuildFloor(Transform parent)
        {
            var floor = SpriteFactory.NewRenderer("Floor", parent, SpriteFactory.Square(),
                Palette.Floor, order: -20);
            floor.transform.localScale = new Vector3(ArenaBounds.x * 2f, ArenaBounds.y * 2f, 1f);

            const float spacing = 2f;
            for (float x = -ArenaBounds.x; x <= ArenaBounds.x + 0.01f; x += spacing)
            {
                var line = SpriteFactory.NewRenderer("GridV", parent, SpriteFactory.Square(),
                    Palette.FloorGrid, order: -19);
                line.transform.localScale = new Vector3(0.03f, ArenaBounds.y * 2f, 1f);
                line.transform.localPosition = new Vector3(x, 0f, 0f);
            }
            for (float y = -ArenaBounds.y; y <= ArenaBounds.y + 0.01f; y += spacing)
            {
                var line = SpriteFactory.NewRenderer("GridH", parent, SpriteFactory.Square(),
                    Palette.FloorGrid, order: -19);
                line.transform.localScale = new Vector3(ArenaBounds.x * 2f, 0.03f, 1f);
                line.transform.localPosition = new Vector3(0f, y, 0f);
            }

            // Edge markers, so the invisible clamp at the boundary is not a surprise.
            var edgeColor = Palette.Wall;
            AddEdge(parent, new Vector3(0f, ArenaBounds.y, 0f), new Vector3(ArenaBounds.x * 2f, 0.12f, 1f), edgeColor);
            AddEdge(parent, new Vector3(0f, -ArenaBounds.y, 0f), new Vector3(ArenaBounds.x * 2f, 0.12f, 1f), edgeColor);
            AddEdge(parent, new Vector3(-ArenaBounds.x, 0f, 0f), new Vector3(0.12f, ArenaBounds.y * 2f, 1f), edgeColor);
            AddEdge(parent, new Vector3(ArenaBounds.x, 0f, 0f), new Vector3(0.12f, ArenaBounds.y * 2f, 1f), edgeColor);
        }

        static void AddEdge(Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var sr = SpriteFactory.NewRenderer("Edge", parent, SpriteFactory.Square(), color, order: -18);
            sr.transform.localPosition = pos;
            sr.transform.localScale = scale;
        }

        void FitCamera()
        {
            float aspect = _camera.aspect > 0.01f ? _camera.aspect : 0.5625f;
            // Show the whole arena on any aspect: whichever axis is tighter wins.
            float byHeight = ArenaBounds.y + 0.8f;
            float byWidth = (ArenaBounds.x + 0.6f) / aspect;
            _camera.orthographicSize = Mathf.Max(byHeight, byWidth);
        }

        void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            _hudCanvas = UIKit.CreateCanvas("HUDCanvas", 10);
            _hudCanvas.transform.SetParent(transform, false);

            _overlayCanvas = UIKit.CreateCanvas("OverlayCanvas", 50);
            _overlayCanvas.transform.SetParent(transform, false);

            _stick = VirtualStick.Create((RectTransform)_hudCanvas.transform, _hudCanvas);
            _stick.gameObject.SetActive(false);

            _graftCard = GraftChoiceCard.Create(_overlayCanvas);
            _endScreen = RunEndScreen.Create(_overlayCanvas);
            _metaScreen = MetaScreen.Create(_overlayCanvas);
        }

        // ---- Run lifecycle -------------------------------------------------

        public void StartRun()
        {
            // Hide the lab here rather than relying on the caller. The button does it too,
            // but a run started from anywhere else must not leave the meta screen covering
            // the arena -- which is exactly what the screenshot tour caught.
            _metaScreen.Hide();

            _resolvingEnd = false;
            _pendingOffers.Clear();
            Time.timeScale = 1f;

            var vessel = VesselCatalog.ById(_profile.EquippedVesselId);
            var mastery = _profile.MasteryFor(vessel.Id);
            var weapon = _profile.EquippedWeapon();

            Run = new RunState(vessel, mastery, weapon);

            // Player
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_arena.transform, false);
            playerGo.transform.position = Vector3.zero;

            _avatar = playerGo.AddComponent<PlayerAvatar>();
            _avatar.Build(vessel);

            _abilities = playerGo.AddComponent<AbilityRunner>();
            _player = playerGo.AddComponent<PlayerController>();

            _arena.Player = playerGo.transform;
            _abilities.Init(this, _player, _fxParent);
            _player.Init(this, _avatar, _abilities, _stick);

            // Director
            var directorGo = new GameObject("WaveDirector");
            directorGo.transform.SetParent(_arena.transform, false);
            _director = directorGo.AddComponent<WaveDirector>();
            _director.Init(this, _enemyParent);

            // HUD
            _hud = CombatHUD.Create(_hudCanvas, Run, _director, _player);
            _stick.gameObject.SetActive(true);

            _runActive = true;
        }

        void Update()
        {
            if (!_runActive || Run == null) return;

            if (!_graftCard.IsOpen && Time.timeScale > 0f)
            {
                Run.Tick(Time.deltaTime);
                _avatar.SetCorruption(Run.Purity.Corruption);
            }

            if (!_resolvingEnd && Run.IsDead) { EndRun(cleared: false); return; }
            if (!_resolvingEnd && _director.RunCleared) { EndRun(cleared: true); return; }

            TryOfferGraft();
        }

        /// <summary>A filled meter opens the choice card. Several can fill at once in a busy
        /// wave, so they queue rather than overwrite each other.</summary>
        void TryOfferGraft()
        {
            if (_graftCard.IsOpen || _resolvingEnd) return;

            var ready = Run.Essence.ReadyArchetypes();
            for (int i = 0; i < ready.Count; i++)
            {
                if (!_pendingOffers.Contains(ready[i])) _pendingOffers.Enqueue(ready[i]);
            }

            while (_pendingOffers.Count > 0)
            {
                var next = _pendingOffers.Peek();
                if (!Run.Essence.IsReady(next)) { _pendingOffers.Dequeue(); continue; }

                _graftCard.Show(Run, next, OnGraftChosen, OnRerollRequested);
                return;
            }
        }

        void OnGraftChosen(GraftOffer offer)
        {
            if (_pendingOffers.Count > 0) _pendingOffers.Dequeue();

            Run.Essence.ConsumeGraftCharge(offer.Source);
            Run.InstallGraft(offer);
            _avatar.SetGraft(offer.Slot, Run.GraftIn(offer.Slot));
            _avatar.SetCorruption(Run.Purity.Corruption);
        }

        /// <summary>
        /// Rewarded-ad reroll (GDD S9). No ad SDK is wired up -- this grants the reroll
        /// directly, and is the single place an SDK callback would slot in later.
        /// </summary>
        void OnRerollRequested()
        {
            _graftCard.Close();

            var ready = Run.Essence.ReadyArchetypes();
            if (ready.Count <= 1) { TryOfferGraft(); return; }

            // Move to the back of the queue so the next ready family is offered instead.
            if (_pendingOffers.Count > 0)
            {
                var skipped = _pendingOffers.Dequeue();
                _pendingOffers.Enqueue(skipped);
            }
            TryOfferGraft();
        }

        public void EndRun(bool cleared)
        {
            _resolvingEnd = true;
            _runActive = false;

            int gold = Run.SoftGoldEarned();
            if (cleared) gold = Mathf.RoundToInt(gold * 1.5f);

            _profile.SoftGold += gold;
            _profile.TotalRuns++;
            _profile.WeaponCores += 2 + Run.Wave;
            if (cleared)
            {
                _profile.VesselShards += 15;
                if (Run.Wave > _profile.HighestWaveCleared) _profile.HighestWaveCleared = Run.Wave;
            }
            SaveProfile();

            _endScreen.Show(Run, cleared, gold, ReturnToMeta);
        }

        void ReturnToMeta()
        {
            TeardownRun();
            _metaScreen.Show(_profile, StartRun, SaveProfile);
        }

        void TeardownRun()
        {
            _arena.ClearAll();

            if (_director != null) Destroy(_director.gameObject);
            if (_arena.Player != null) Destroy(_arena.Player.gameObject);
            _arena.Player = null;

            if (_hud != null) Destroy(_hud);
            for (int i = _fxParent.childCount - 1; i >= 0; i--) Destroy(_fxParent.GetChild(i).gameObject);

            _stick.gameObject.SetActive(false);
            Run = null;
        }

        void SaveProfile() => _saves.Save(_profile);

        // ---- RunContext ----------------------------------------------------

        public void DamagePlayer(float amount)
        {
            if (Run == null || Run.IsDead) return;
            Run.TakeDamage(amount);
            _avatar.FlashHit();
        }

        public void OnEnemyKilled(EnemyAgent enemy)
        {
            if (Run == null) return;
            // The drop is the point: essence shaped like whatever just died.
            int amount = enemy.IsBoss ? 4 : 1;
            for (int i = 0; i < amount; i++)
                EssenceOrb.Spawn(this, enemy.Archetype, enemy.transform.position, _fxParent);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveProfile();
        }

        void OnApplicationQuit() => SaveProfile();
    }
}
