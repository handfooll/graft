using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Graft.Core;
using Graft.Game;
using Graft.App;

namespace Graft.Tests.PlayMode
{
    /// <summary>
    /// Dev-only: walks the game through its states and saves a PNG of each, so "does this
    /// actually look right" can be answered without opening the editor. Compiling clean and
    /// passing logic tests says nothing about whether anything is visible on screen.
    ///
    /// Run with graphics (no -nographics):
    ///   Unity -batchmode -projectPath . -runTests -testPlatform PlayMode
    /// It skips itself when there is no graphics device rather than failing the suite.
    /// </summary>
    [Category("Screenshots")]
    public class ScreenshotTour
    {
        const int Width = 720;
        const int Height = 1280;

        static string OutDir => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "Screenshots");

        Camera _camera;
        RenderTexture _rt;

        [UnityTest]
        public IEnumerator Capture_every_screen()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (running with -nographics); skipping the tour.");
                yield break;
            }

            for (int i = 0; i < 10 && GameFlow.Instance == null; i++) yield return null;
            var flow = GameFlow.Instance;
            Assert.IsNotNull(flow);

            Directory.CreateDirectory(OutDir);
            Time.timeScale = 1f;

            _camera = Camera.main;
            Assert.IsNotNull(_camera, "No main camera to capture from.");
            _rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _rt;

            // Overlay canvases do not render into a camera target, so route them through
            // the capture camera for the duration of the tour.
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = _camera;
                c.planeDistance = 1f;
            }

            yield return Settle();
            yield return Save("01_meta_lab");

            flow.StartRun();
            yield return Settle();
            yield return Save("02_run_start");

            // Let a wave build up so the arena is not empty.
            yield return WaitRealtime(4.5f);
            yield return Save("03_combat");

            // The signature moment.
            var run = flow.Run;
            // The card starts inactive, so an active-only search will not find it.
            var card = Object.FindAnyObjectByType<Graft.UI.GraftChoiceCard>(FindObjectsInactive.Include);
            Assert.IsNotNull(card, "No graft choice card was built.");
            card.Show(run, ArchetypeCatalog.Venomspitter, _ => { }, () => { });
            yield return Settle();
            yield return Save("04_graft_choice");
            card.Close();

            // A built hero: fill every slot so the body is visibly grown.
            InstallEverywhere(flow);
            yield return Settle();
            yield return WaitRealtime(1.2f);
            yield return Save("05_grafted_hero");

            // Push to Monstrous so the mutation and the burn warning are visible.
            run.Purity.Add(0.95f);
            flow.Avatar.SetCorruption(run.Purity.Corruption);
            yield return WaitRealtime(2.0f);
            yield return Save("06_monstrous");

            // Boss.
            Arena.Instance.ClearAll();
            var bossGo = new GameObject("TourBoss");
            bossGo.transform.position = new Vector3(0f, 4f, 0f);
            bossGo.AddComponent<EnemyAgent>().Init(flow, ArchetypeCatalog.HollowChoir, 1f, true);
            yield return Settle();
            yield return Save("07_boss");

            // Run end.
            flow.EndRun(cleared: true);
            yield return Settle();
            yield return Save("08_run_end");

            _camera.targetTexture = null;
            Debug.Log("[GRAFT] Screenshot tour written to " + OutDir);
        }

        static void InstallEverywhere(GameFlow flow)
        {
            var run = flow.Run;
            var picks = new[]
            {
                (ArchetypeCatalog.Venomspitter, GraftSlot.Head),
                (ArchetypeCatalog.Hidebrute, GraftSlot.Core),
                (ArchetypeCatalog.Sparkmite, GraftSlot.LeftArm),
                (ArchetypeCatalog.Bloodcharger, GraftSlot.RightArm),
                (ArchetypeCatalog.Sparkmite, GraftSlot.Legs)
            };

            foreach (var (archetype, slot) in picks)
            {
                var offers = run.BuildOffers(archetype);
                var offer = offers.Find(o => o.Slot == slot);
                run.InstallGraft(offer);
                flow.Avatar.SetGraft(slot, run.GraftIn(slot));
            }
            flow.Avatar.SetCorruption(run.Purity.Corruption);
        }

        /// <summary>Animations pop in over ~0.22s; capturing immediately catches everything
        /// mid-scale, which reads in the PNG as a layout bug when nothing is wrong.</summary>
        static IEnumerator Settle()
        {
            for (int i = 0; i < 30; i++) yield return null;
        }

        static IEnumerator WaitRealtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Save(string name)
        {
            // Deliberately not WaitForEndOfFrame: it never fires under -batchmode and hangs
            // the run forever. Render() is an explicit draw, so we do not need the frame end.
            yield return null;

            Canvas.ForceUpdateCanvases();
            _camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            var path = Path.Combine(OutDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("[GRAFT] captured " + path);
        }
    }
}
