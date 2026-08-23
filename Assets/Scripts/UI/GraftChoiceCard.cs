using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Graft.Core;
using Graft.Game;

namespace Graft.UI
{
    /// <summary>
    /// The level-up moment, and the reason this game is not XP Hero: the card does not offer
    /// three random upgrades, it offers *one absorbed creature* and asks where on your body
    /// it goes. Every row is the same trait delivered a different way, so the decision is
    /// about your build rather than about which number is largest.
    /// </summary>
    public sealed class GraftChoiceCard : MonoBehaviour
    {
        RunState _run;
        EnemyArchetype _source;
        Action<GraftOffer> _onChosen;
        Action _onRerollRequested;
        RectTransform _root;
        readonly List<GameObject> _rows = new List<GameObject>();

        public static GraftChoiceCard Create(Canvas canvas)
        {
            var go = new GameObject("GraftChoiceCard", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var card = go.AddComponent<GraftChoiceCard>();
            card._root = (RectTransform)go.transform;
            UIKit.Stretch(card._root);
            go.SetActive(false);
            return card;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Show(RunState run, EnemyArchetype source, Action<GraftOffer> onChosen,
            Action onRerollRequested)
        {
            _run = run;
            _source = source;
            _onChosen = onChosen;
            _onRerollRequested = onRerollRequested;

            Rebuild();
            gameObject.SetActive(true);
            Time.timeScale = 0f;
        }

        public void Close()
        {
            Time.timeScale = 1f;
            gameObject.SetActive(false);
        }

        void Rebuild()
        {
            for (int i = 0; i < _rows.Count; i++) Destroy(_rows[i]);
            _rows.Clear();

            var dim = UIKit.Panel("Dim", _root, new Color(0f, 0f, 0f, 0.78f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            _rows.Add(dim.gameObject);

            var panel = UIKit.Panel("Panel", _root, Palette.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 1480f));
            _rows.Add(panel.gameObject);

            var accent = Palette.For(_source.Element);

            // Header: what you absorbed, and what it costs you to keep it.
            var header = UIKit.Panel("Header", panel.transform, new Color(accent.r, accent.g, accent.b, 0.20f));
            UIKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(960f, 210f));

            var title = UIKit.Label("Title", header.transform,
                _source.DisplayName.ToUpperInvariant() + " ESSENCE", 46,
                TextAnchor.UpperCenter, accent, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(920f, 56f));

            var blurb = UIKit.Label("Blurb", header.transform, _source.TraitBlurb, 26,
                TextAnchor.UpperCenter, Palette.Text);
            UIKit.Place(blurb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -74f), new Vector2(900f, 60f));

            float cost = _run.Purity.PreviewCost(_source);
            float projected = Mathf.Clamp01(_run.Purity.Corruption + cost);
            var projectedBand = PurityMeter.BandFor(projected);
            string warn = projectedBand != _run.Purity.Band
                ? "   ->  " + projectedBand.ToString().ToUpperInvariant()
                : "";

            var costLabel = UIKit.Label("Cost", header.transform,
                "GRAFTING COSTS +" + Mathf.RoundToInt(cost * 100f) + "% CORRUPTION" + warn, 24,
                TextAnchor.UpperCenter, Palette.For(projectedBand), FontStyle.Bold);
            UIKit.Place(costLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(920f, 44f));

            var prompt = UIKit.Label("Prompt", panel.transform, "WHERE DOES IT GO?", 30,
                TextAnchor.MiddleCenter, Palette.TextDim, FontStyle.Bold);
            UIKit.Place(prompt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -244f), new Vector2(920f, 40f));

            // One row per slot.
            var offers = _run.BuildOffers(_source);
            float y = -296f;
            const float rowHeight = 196f;

            for (int i = 0; i < offers.Count; i++)
            {
                BuildRow(panel.transform, offers[i], y, rowHeight, accent);
                y -= rowHeight + 12f;
            }

            // Rewarded-ad reroll (GDD S9): the graft-choice moment is exactly where a
            // player wants a do-over, which is what makes it a good ad placement.
            var reroll = UIKit.Button("Reroll", panel.transform, "WATCH AD  ->  REROLL",
                new Color(0.36f, 0.32f, 0.20f), () => _onRerollRequested?.Invoke(), 26);
            UIKit.Place(reroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(960f, 74f));
        }

        void BuildRow(Transform parent, GraftOffer offer, float y, float height, Color accent)
        {
            var spec = offer.Resolved;

            Color fill = offer.IsReinforcement
                ? new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 0.55f)
                : new Color(0.17f, 0.16f, 0.22f, 0.95f);

            var btn = UIKit.Button("Offer_" + offer.Slot, parent, "", fill, () =>
            {
                Close();
                _onChosen?.Invoke(offer);
            });
            // The generated caption is empty; we lay out richer text on top of it instead.
            var caption = btn.transform.Find("Offer_" + offer.Slot + "_Label");
            if (caption != null) caption.gameObject.SetActive(false);

            var rt = btn.GetComponent<RectTransform>();
            UIKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(960f, height));

            var slotName = UIKit.Label("Slot", rt, SlotTitle(offer.Slot), 34,
                TextAnchor.UpperLeft, accent, FontStyle.Bold);
            UIKit.Place(slotName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -12f), new Vector2(520f, 40f));

            var abilityName = UIKit.Label("Ability", rt, spec.DisplayName, 26,
                TextAnchor.UpperLeft, Palette.Text);
            UIKit.Place(abilityName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -54f), new Vector2(700f, 34f));

            var desc = UIKit.Label("Desc", rt, spec.Description, 22,
                TextAnchor.UpperLeft, Palette.TextDim);
            UIKit.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -90f), new Vector2(700f, 60f));

            // The consequence line. Overwriting a graft that carried the run is the
            // late-game decision the GDD is after, so it must never be a surprise.
            string consequence;
            Color consequenceColor;
            if (offer.IsReinforcement)
            {
                consequence = "REINFORCE  ->  TIER " + (offer.Resolved.Damage > 0 ? "UP" : "UP");
                consequenceColor = new Color(0.55f, 0.90f, 0.55f);
            }
            else if (offer.Replaces != null)
            {
                consequence = "REPLACES " + offer.Replaces.Source.DisplayName.ToUpperInvariant();
                consequenceColor = new Color(0.95f, 0.55f, 0.40f);
            }
            else
            {
                consequence = "EMPTY SLOT";
                consequenceColor = Palette.TextDim;
            }

            var cons = UIKit.Label("Consequence", rt, consequence, 22,
                TextAnchor.LowerLeft, consequenceColor, FontStyle.Bold);
            UIKit.Place(cons.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 12f), new Vector2(700f, 30f));

            var dmg = UIKit.Label("Dmg", rt, Mathf.RoundToInt(spec.Damage).ToString(), 46,
                TextAnchor.MiddleRight, accent, FontStyle.Bold);
            UIKit.Place(dmg.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 12f), new Vector2(180f, 56f));

            var dmgTag = UIKit.Label("DmgTag", rt, "DMG", 18,
                TextAnchor.MiddleRight, Palette.TextDim);
            UIKit.Place(dmgTag.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, -26f), new Vector2(180f, 24f));
        }

        static string SlotTitle(GraftSlot slot)
        {
            switch (slot)
            {
                case GraftSlot.Head: return "HEAD";
                case GraftSlot.Core: return "CORE";
                case GraftSlot.LeftArm: return "LEFT ARM";
                case GraftSlot.RightArm: return "RIGHT ARM";
                default: return "LEGS";
            }
        }
    }
}
