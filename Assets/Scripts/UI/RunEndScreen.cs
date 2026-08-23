using System;
using UnityEngine;
using Graft.Core;
using Graft.Game;

namespace Graft.UI
{
    /// <summary>
    /// End of a run: what you became, what you earned. Shows the final build explicitly,
    /// because the thing worth remembering about a run in this design is the body you
    /// ended up with, not the score.
    /// </summary>
    public sealed class RunEndScreen : MonoBehaviour
    {
        RectTransform _root;
        Action _onContinue;

        public static RunEndScreen Create(Canvas canvas)
        {
            var go = new GameObject("RunEndScreen", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var s = go.AddComponent<RunEndScreen>();
            s._root = (RectTransform)go.transform;
            UIKit.Stretch(s._root);
            go.SetActive(false);
            return s;
        }

        public void Show(RunState run, bool cleared, int goldEarned, Action onContinue)
        {
            _onContinue = onContinue;

            for (int i = _root.childCount - 1; i >= 0; i--) Destroy(_root.GetChild(i).gameObject);

            var dim = UIKit.Panel("Dim", _root, new Color(0f, 0f, 0f, 0.85f));
            UIKit.Stretch(dim.rectTransform);

            var panel = UIKit.Panel("Panel", _root, Palette.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(980f, 820f));

            var headline = cleared ? "BOSS DOWN" : "YOU DIED";
            var headlineColor = cleared ? Palette.Gold : Palette.Health;

            var title = UIKit.Label("Title", panel.transform, headline, 68,
                TextAnchor.UpperCenter, headlineColor, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(900f, 80f));

            var band = run.Purity.Band;
            var sub = UIKit.Label("Sub", panel.transform,
                "You finished " + band.ToString().ToUpperInvariant() + " at " +
                Mathf.RoundToInt(run.Purity.Corruption * 100f) + "% corruption", 28,
                TextAnchor.UpperCenter, Palette.For(band));
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -122f), new Vector2(900f, 40f));

            // The build you ended with.
            float y = -190f;
            var buildTitle = UIKit.Label("BuildTitle", panel.transform, "FINAL BODY", 26,
                TextAnchor.UpperLeft, Palette.TextDim, FontStyle.Bold);
            UIKit.Place(buildTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(50f, y), new Vector2(880f, 34f));
            y -= 44f;

            foreach (var slot in RunState.AllSlots)
            {
                var g = run.GraftIn(slot);
                string text = g == null
                    ? SlotName(slot) + " -- empty"
                    : SlotName(slot) + " -- " + g.Source.DisplayName +
                      (g.Tier > 1 ? "  (tier " + g.Tier + ")" : "");
                var color = g == null ? Palette.TextDim : Palette.For(g.Source.Element);

                var row = UIKit.Label("Row_" + slot, panel.transform, text, 28,
                    TextAnchor.UpperLeft, color);
                UIKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(50f, y), new Vector2(880f, 38f));
                y -= 46f;
            }

            y -= 26f;
            var stats = UIKit.Label("Stats", panel.transform,
                "Wave " + run.Wave + "     " + run.Kills + " kills     " +
                UIKit.FormatTime(run.Elapsed), 28,
                TextAnchor.UpperLeft, Palette.Text);
            UIKit.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(50f, y), new Vector2(880f, 38f));
            y -= 60f;

            var gold = UIKit.Label("Gold", panel.transform,
                "+ " + goldEarned + " SOFT GOLD", 40,
                TextAnchor.UpperLeft, Palette.Gold, FontStyle.Bold);
            UIKit.Place(gold.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(50f, y), new Vector2(880f, 50f));

            var btn = UIKit.Button("Continue", panel.transform, "BACK TO THE LAB",
                Palette.Gold, () => { gameObject.SetActive(false); _onContinue?.Invoke(); });
            UIKit.Place(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(880f, 96f));

            gameObject.SetActive(true);
            Time.timeScale = 0f;
        }

        static string SlotName(GraftSlot slot)
        {
            switch (slot)
            {
                case GraftSlot.Head: return "Head";
                case GraftSlot.Core: return "Core";
                case GraftSlot.LeftArm: return "Left arm";
                case GraftSlot.RightArm: return "Right arm";
                default: return "Legs";
            }
        }
    }
}
