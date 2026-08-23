using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Graft.Core;
using Graft.Game;

namespace Graft.UI
{
    /// <summary>
    /// The between-runs layer (GDD S3 meta loop, S6, S7): pick the body, pick the chassis,
    /// spend Soft Gold on the mastery tree, merge duplicate weapons. Rebuilt from scratch
    /// on every change rather than diffed -- the screen is small and correctness beats
    /// cleverness here.
    /// </summary>
    public sealed class MetaScreen : MonoBehaviour
    {
        PlayerProfile _profile;
        Action _onStartRun;
        Action _onProfileChanged;
        RectTransform _root;
        RectTransform _scrollContent;

        public static MetaScreen Create(Canvas canvas)
        {
            var go = new GameObject("MetaScreen", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var s = go.AddComponent<MetaScreen>();
            s._root = (RectTransform)go.transform;
            UIKit.Stretch(s._root);
            go.SetActive(false);
            return s;
        }

        public void Show(PlayerProfile profile, Action onStartRun, Action onProfileChanged)
        {
            _profile = profile;
            _onStartRun = onStartRun;
            _onProfileChanged = onProfileChanged;
            gameObject.SetActive(true);
            Time.timeScale = 1f;
            Rebuild();
        }

        public void Hide() => gameObject.SetActive(false);

        void Rebuild()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) Destroy(_root.GetChild(i).gameObject);

            var bg = UIKit.Panel("BG", _root, new Color(0.07f, 0.06f, 0.09f, 1f));
            UIKit.Stretch(bg.rectTransform);

            var title = UIKit.Label("Title", _root, "GRAFT", 92,
                TextAnchor.UpperCenter, Palette.Health, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(900f, 100f));

            var purse = UIKit.Label("Purse", _root,
                _profile.SoftGold + " GOLD     " + _profile.WeaponCores + " CORES     " +
                _profile.VesselShards + " SHARDS", 26,
                TextAnchor.UpperCenter, Palette.Gold);
            UIKit.Place(purse.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -142f), new Vector2(900f, 34f));

            BuildScrollArea();

            var start = UIKit.Button("Start", _root, "ENTER THE ARENA", Palette.Health, () =>
            {
                Hide();
                _onStartRun?.Invoke();
            }, 40);
            UIKit.Place(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(940f, 120f));
        }

        void BuildScrollArea()
        {
            var viewport = UIKit.Panel("Viewport", _root, new Color(0f, 0f, 0f, 0f));
            UIKit.Place(viewport.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -20f), new Vector2(1000f, 1420f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28f;

            var content = UIKit.Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            // A fresh RectTransform carries sizeDelta (100,100). With horizontal stretch
            // anchors that makes the content 100 units WIDER than the viewport, clipping
            // every row on both edges. Zero it so the width tracks the viewport exactly;
            // ContentSizeFitter still drives the height.
            content.sizeDelta = Vector2.zero;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.VStack(content, 14f, 10f);

            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            _scrollContent = content;

            BuildVesselSection(content);
            BuildWeaponSection(content);
            BuildMasterySection(content);
        }

        void SectionHeader(RectTransform parent, string text)
        {
            var lbl = UIKit.Label("Header_" + text, parent, text, 32,
                TextAnchor.MiddleLeft, Palette.TextDim, FontStyle.Bold);
            UIKit.Sized(lbl.gameObject, 44f);
        }

        void BuildVesselSection(RectTransform parent)
        {
            SectionHeader(parent, "VESSEL  -  the body grafts attach to");

            foreach (var vessel in VesselCatalog.All)
            {
                bool owned = _profile.HasVessel(vessel.Id);
                bool equipped = _profile.EquippedVesselId == vessel.Id;

                var rarity = Palette.For(vessel.Rarity);
                Color fill = equipped
                    ? new Color(rarity.r * 0.5f, rarity.g * 0.5f, rarity.b * 0.5f, 0.9f)
                    : new Color(0.14f, 0.13f, 0.18f, 0.95f);

                var btn = UIKit.Button("Vessel_" + vessel.Id, parent, "", fill, () =>
                {
                    if (owned)
                    {
                        _profile.EquippedVesselId = vessel.Id;
                        _onProfileChanged?.Invoke();
                        Rebuild();
                    }
                    else if (_profile.VesselShards >= UnlockCost(vessel))
                    {
                        _profile.VesselShards -= UnlockCost(vessel);
                        _profile.UnlockedVesselIds.Add(vessel.Id);
                        _profile.MasteryFor(vessel.Id);
                        _onProfileChanged?.Invoke();
                        Rebuild();
                    }
                });
                UIKit.Sized(btn.gameObject, 168f);
                HideGeneratedCaption(btn, "Vessel_" + vessel.Id);

                var rt = btn.GetComponent<RectTransform>();

                var name = UIKit.Label("Name", rt, vessel.DisplayName.ToUpperInvariant() +
                    "   " + vessel.Rarity.ToString().ToUpperInvariant(), 32,
                    TextAnchor.UpperLeft, rarity, FontStyle.Bold);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(18f, -12f), new Vector2(760f, 38f));

                var blurb = UIKit.Label("Blurb", rt, vessel.IdentityBlurb, 22,
                    TextAnchor.UpperLeft, Palette.TextDim);
                UIKit.Place(blurb.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(18f, -54f), new Vector2(720f, 56f));

                string affinity = vessel.Affinity.HasValue
                    ? vessel.Affinity.Value.ToString().ToUpperInvariant() + " AFFINITY  +" +
                      Mathf.RoundToInt(vessel.AffinityBonus * 100f) + "%"
                    : "NO AFFINITY";
                string resist = "CORRUPTION RESIST " +
                                (vessel.CorruptionResistance >= 0f ? "+" : "") +
                                Mathf.RoundToInt(vessel.CorruptionResistance * 100f) + "%";

                var tags = UIKit.Label("Tags", rt, affinity + "     " + resist, 20,
                    TextAnchor.LowerLeft,
                    vessel.Affinity.HasValue ? Palette.For(vessel.Affinity.Value) : Palette.TextDim,
                    FontStyle.Bold);
                UIKit.Place(tags.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(18f, 14f), new Vector2(760f, 28f));

                string state = equipped ? "EQUIPPED" : owned ? "TAP TO EQUIP" :
                    UnlockCost(vessel) + " SHARDS";
                var stateLabel = UIKit.Label("State", rt, state, 24,
                    TextAnchor.MiddleRight, equipped ? Palette.Gold : Palette.TextDim, FontStyle.Bold);
                UIKit.Place(stateLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-20f, 0f), new Vector2(240f, 40f));
            }
        }

        static int UnlockCost(VesselDefinition v) => 20 + 30 * (int)v.Rarity;

        void BuildWeaponSection(RectTransform parent)
        {
            SectionHeader(parent, "RELIC WEAPON  -  reshapes every graft");

            var stack = _profile.WeaponStack();
            // Group by (definition, tier) so the merge affordance is obvious.
            var seen = new HashSet<string>();

            for (int i = 0; i < stack.Count; i++)
            {
                var w = stack[i];
                string key = w.DefinitionId + "|" + (int)w.Tier;
                if (!seen.Add(key)) continue;

                int count = WeaponMerge.CountAt(stack, w.DefinitionId, w.Tier);
                var def = w.Definition;
                bool equipped = _profile.EquippedWeaponId == w.DefinitionId &&
                                _profile.EquippedWeaponTier == (int)w.Tier;
                var rarity = Palette.For(w.Tier);

                Color fill = equipped
                    ? new Color(rarity.r * 0.5f, rarity.g * 0.5f, rarity.b * 0.5f, 0.9f)
                    : new Color(0.14f, 0.13f, 0.18f, 0.95f);

                var btn = UIKit.Button("Weapon_" + key, parent, "", fill, () =>
                {
                    _profile.EquippedWeaponId = w.DefinitionId;
                    _profile.EquippedWeaponTier = (int)w.Tier;
                    _onProfileChanged?.Invoke();
                    Rebuild();
                });
                UIKit.Sized(btn.gameObject, 132f);
                HideGeneratedCaption(btn, "Weapon_" + key);
                var rt = btn.GetComponent<RectTransform>();

                var name = UIKit.Label("Name", rt,
                    def.DisplayName.ToUpperInvariant() + "  " + w.Tier.ToString().ToUpperInvariant() +
                    (count > 1 ? "  x" + count : ""), 30,
                    TextAnchor.UpperLeft, rarity, FontStyle.Bold);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(18f, -12f), new Vector2(700f, 36f));

                string shape = def.Class == WeaponClass.Blade
                    ? "BLADE  -  grafts become wide melee sweeps"
                    : "FIREARM  -  grafts become projectile spreads";
                var shapeLabel = UIKit.Label("Shape", rt, shape, 21,
                    TextAnchor.UpperLeft, Palette.TextDim);
                UIKit.Place(shapeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(18f, -50f), new Vector2(700f, 30f));

                var power = UIKit.Label("Power", rt,
                    "POWER " + Mathf.RoundToInt(w.Power) + "     LV " + w.Level, 22,
                    TextAnchor.LowerLeft, Palette.Text);
                UIKit.Place(power.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(18f, 12f), new Vector2(500f, 28f));

                if (equipped)
                {
                    var eq = UIKit.Label("Eq", rt, "EQUIPPED", 22,
                        TextAnchor.UpperRight, Palette.Gold, FontStyle.Bold);
                    UIKit.Place(eq.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-20f, -14f), new Vector2(220f, 30f));
                }

                if (WeaponMerge.CanMerge(stack, w.DefinitionId, w.Tier))
                {
                    var mergeBtn = UIKit.Button("Merge_" + key, rt, "MERGE 3", Palette.Gold, () =>
                    {
                        var live = _profile.WeaponStack();
                        WeaponMerge.Merge(live, w.DefinitionId, w.Tier);
                        _profile.StoreWeaponStack(live);
                        _onProfileChanged?.Invoke();
                        Rebuild();
                    }, 24);
                    UIKit.Place(mergeBtn.GetComponent<RectTransform>(),
                        new Vector2(1f, 0f), new Vector2(1f, 0f),
                        new Vector2(-20f, 14f), new Vector2(200f, 52f));
                }
            }
        }

        void BuildMasterySection(RectTransform parent)
        {
            var vessel = VesselCatalog.ById(_profile.EquippedVesselId);
            SectionHeader(parent, "VESSEL MASTERY  -  " + vessel.DisplayName.ToUpperInvariant());

            var mastery = _profile.MasteryFor(vessel.Id);

            foreach (var node in VesselMastery.AllNodes)
            {
                int level = mastery.LevelOf(node);
                bool maxed = mastery.IsMaxed(node);
                int cost = mastery.CostToUpgrade(node);
                bool affordable = !maxed && _profile.SoftGold >= cost;

                var row = UIKit.Panel("Node_" + node, parent, new Color(0.13f, 0.12f, 0.17f, 0.95f));
                UIKit.Sized(row.gameObject, 96f);
                var rt = row.rectTransform;

                var name = UIKit.Label("Name", rt, VesselMastery.DisplayName(node).ToUpperInvariant(), 28,
                    TextAnchor.UpperLeft, Palette.Text, FontStyle.Bold);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(18f, -12f), new Vector2(560f, 34f));

                var lvl = UIKit.Label("Lvl", rt, "LEVEL " + level + " / " + VesselMastery.MaxLevel, 22,
                    TextAnchor.LowerLeft, Palette.TextDim);
                UIKit.Place(lvl.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(18f, 12f), new Vector2(560f, 28f));

                // Progress bar so the tree reads at a glance rather than as a wall of numbers.
                var track = UIKit.Panel("Track", rt, new Color(0f, 0f, 0f, 0.4f));
                UIKit.Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(300f, -14f), new Vector2(320f, 14f));
                var fill = UIKit.Bar("Fill", track.transform, Palette.Gold);
                UIKit.Stretch(fill.rectTransform, 2f);
                fill.fillAmount = level / (float)VesselMastery.MaxLevel;

                string caption = maxed ? "MAX" : cost + " G";
                var color = maxed ? new Color(0.3f, 0.3f, 0.35f)
                    : affordable ? Palette.Gold : new Color(0.3f, 0.28f, 0.22f);

                var btn = UIKit.Button("Up_" + node, rt, caption, color, () =>
                {
                    int gold = _profile.SoftGold;
                    if (mastery.TryUpgrade(node, ref gold))
                    {
                        _profile.SoftGold = gold;
                        _onProfileChanged?.Invoke();
                        Rebuild();
                    }
                }, 26);
                btn.interactable = affordable;
                UIKit.Place(btn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-18f, 0f), new Vector2(220f, 68f));
            }
        }

        static void HideGeneratedCaption(Button btn, string name)
        {
            var caption = btn.transform.Find(name + "_Label");
            if (caption != null) caption.gameObject.SetActive(false);
        }
    }
}
