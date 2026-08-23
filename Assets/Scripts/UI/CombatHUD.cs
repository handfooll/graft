using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Graft.Core;
using Graft.Game;

namespace Graft.UI
{
    /// <summary>
    /// The in-run HUD. Two things are on screen permanently because the GDD's hook depends
    /// on them being readable at a glance: the five graft slots, and the Purity meter.
    /// Everything else is sized down around those.
    /// </summary>
    public sealed class CombatHUD : MonoBehaviour
    {
        RunState _run;
        WaveDirector _director;
        PlayerController _player;

        Image _healthFill;
        Text _healthText;
        Image _purityFill;
        Text _purityLabel;
        Text _waveText;
        Text _timerText;
        Image _dashCooldown;

        readonly Dictionary<GraftSlot, Image> _slotFills = new Dictionary<GraftSlot, Image>();
        readonly Dictionary<GraftSlot, Text> _slotLabels = new Dictionary<GraftSlot, Text>();
        readonly Dictionary<string, Image> _essenceFills = new Dictionary<string, Image>();

        public static CombatHUD Create(Canvas canvas, RunState run, WaveDirector director,
            PlayerController player)
        {
            var hud = canvas.gameObject.AddComponent<CombatHUD>();
            hud._run = run;
            hud._director = director;
            hud._player = player;
            hud.Build((RectTransform)canvas.transform);
            return hud;
        }

        void Build(RectTransform root)
        {
            BuildTopBar(root);
            BuildVitals(root);
            BuildEssenceMeters(root);
            BuildSlotRow(root);
            BuildDashButton(root);
        }

        void BuildTopBar(RectTransform root)
        {
            var bar = UIKit.Rect("TopBar", root);
            UIKit.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(980f, 60f));

            _waveText = UIKit.Label("Wave", bar, "WAVE 1", 40, TextAnchor.MiddleLeft,
                Palette.Text, FontStyle.Bold);
            UIKit.Stretch(_waveText.rectTransform);

            _timerText = UIKit.Label("Timer", bar, "0:00", 40, TextAnchor.MiddleRight, Palette.TextDim);
            UIKit.Stretch(_timerText.rectTransform);
        }

        void BuildVitals(RectTransform root)
        {
            var wrap = UIKit.Rect("Vitals", root);
            UIKit.Place(wrap, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(980f, 108f));

            // Health
            var hpBack = UIKit.Panel("HPBack", wrap, Palette.HealthBack);
            UIKit.Place(hpBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(980f, 44f));
            _healthFill = UIKit.Bar("HPFill", hpBack.transform, Palette.Health);
            UIKit.Stretch(_healthFill.rectTransform, 3f);
            _healthText = UIKit.Label("HPText", hpBack.transform, "", 26, TextAnchor.MiddleCenter,
                Palette.Text, FontStyle.Bold);
            UIKit.Stretch(_healthText.rectTransform);

            // Purity: the meter the whole greed mechanic hangs on.
            var pBack = UIKit.Panel("PurityBack", wrap, new Color(0.12f, 0.12f, 0.18f));
            UIKit.Place(pBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -52f), new Vector2(980f, 40f));
            _purityFill = UIKit.Bar("PurityFill", pBack.transform, Palette.PurityClean);
            UIKit.Stretch(_purityFill.rectTransform, 3f);
            _purityLabel = UIKit.Label("PurityLabel", pBack.transform, "FOCUSED", 24,
                TextAnchor.MiddleCenter, Palette.Text, FontStyle.Bold);
            UIKit.Stretch(_purityLabel.rectTransform);
        }

        /// <summary>One meter per enemy family -- the visual proof that Essence is not a
        /// single XP bar.</summary>
        void BuildEssenceMeters(RectTransform root)
        {
            var wrap = UIKit.Rect("Essence", root);
            UIKit.Place(wrap, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(50f, -240f), new Vector2(300f, 200f));
            UIKit.VStack(wrap, 8f);

            foreach (var a in ArchetypeCatalog.WaveSpawnable)
            {
                var row = UIKit.Rect("Meter_" + a.Id, wrap);
                UIKit.Sized(row.gameObject, 26f);

                var back = UIKit.Panel("Back", row, new Color(0f, 0f, 0f, 0.45f));
                UIKit.Stretch(back.rectTransform);

                var fill = UIKit.Bar("Fill", back.transform, Palette.For(a.Element));
                UIKit.Stretch(fill.rectTransform, 2f);
                fill.fillAmount = 0f;
                _essenceFills[a.Id] = fill;

                var label = UIKit.Label("Name", back.transform, a.DisplayName, 18,
                    TextAnchor.MiddleLeft, Palette.Text);
                UIKit.Stretch(label.rectTransform, 6f);
            }
        }

        void BuildSlotRow(RectTransform root)
        {
            var wrap = UIKit.Rect("Slots", root);
            UIKit.Place(wrap, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(980f, 120f));
            UIKit.HStack(wrap, 12f);

            foreach (var slot in RunState.AllSlots)
            {
                var cell = UIKit.Rect("Slot_" + slot, wrap);
                UIKit.Sized(cell.gameObject, 120f);

                var back = UIKit.Panel("Back", cell, new Color(0.08f, 0.08f, 0.12f, 0.85f));
                UIKit.Stretch(back.rectTransform);

                var fill = UIKit.Panel("Fill", back.transform, new Color(1f, 1f, 1f, 0f));
                UIKit.Stretch(fill.rectTransform, 4f);
                _slotFills[slot] = fill;

                var name = UIKit.Label("Name", back.transform, ShortName(slot), 20,
                    TextAnchor.UpperCenter, Palette.TextDim);
                UIKit.Stretch(name.rectTransform, 6f);

                var value = UIKit.Label("Value", back.transform, "--", 26,
                    TextAnchor.LowerCenter, Palette.Text, FontStyle.Bold);
                UIKit.Stretch(value.rectTransform, 6f);
                _slotLabels[slot] = value;
            }
        }

        void BuildDashButton(RectTransform root)
        {
            var btn = UIKit.Button("Dash", root, "DASH", new Color(0.30f, 0.28f, 0.40f),
                () => _player.RequestDash(), 30);
            UIKit.Place(btn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-50f, 190f), new Vector2(190f, 190f));

            _dashCooldown = UIKit.Panel("CD", btn.transform, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Stretch(_dashCooldown.rectTransform);
            _dashCooldown.type = Image.Type.Filled;
            _dashCooldown.fillMethod = Image.FillMethod.Vertical;
            _dashCooldown.fillOrigin = (int)Image.OriginVertical.Top;
            _dashCooldown.raycastTarget = false;
            _dashCooldown.fillAmount = 0f;
        }

        static string ShortName(GraftSlot slot)
        {
            switch (slot)
            {
                case GraftSlot.Head: return "HEAD";
                case GraftSlot.Core: return "CORE";
                case GraftSlot.LeftArm: return "L ARM";
                case GraftSlot.RightArm: return "R ARM";
                default: return "LEGS";
            }
        }

        void Update()
        {
            if (_run == null) return;

            float max = _run.EffectiveMaxHP;
            _healthFill.fillAmount = max > 0f ? Mathf.Clamp01(_run.CurrentHP / max) : 0f;
            _healthText.text = Mathf.CeilToInt(_run.CurrentHP) + " / " + Mathf.CeilToInt(max);

            float c = _run.Purity.Corruption;
            var band = _run.Purity.Band;
            _purityFill.fillAmount = c;
            _purityFill.color = Palette.For(band);
            _purityLabel.text = band.ToString().ToUpperInvariant() + "   " +
                                Mathf.RoundToInt(c * 100f) + "% CORRUPT" +
                                (_run.Purity.SelfDamageFractionPerSecond > 0f ? "   BURNING" : "");

            _waveText.text = _director.BossActive ? "BOSS" : "WAVE " + _director.CurrentWave;
            _timerText.text = _director.BossActive
                ? "HOLLOW CHOIR"
                : UIKit.FormatTime(_director.TimeLeftInWave);

            foreach (var a in ArchetypeCatalog.WaveSpawnable)
            {
                if (_essenceFills.TryGetValue(a.Id, out var fill))
                    fill.fillAmount = _run.Essence.FillFractionFor(a);
            }

            foreach (var slot in RunState.AllSlots)
            {
                var g = _run.GraftIn(slot);
                if (g == null)
                {
                    _slotFills[slot].color = new Color(1f, 1f, 1f, 0f);
                    _slotLabels[slot].text = "--";
                }
                else
                {
                    var col = Palette.For(g.Source.Element);
                    _slotFills[slot].color = new Color(col.r, col.g, col.b, 0.32f);
                    _slotLabels[slot].text = g.Source.DisplayName.Substring(0, 4).ToUpperInvariant() +
                                             (g.Tier > 1 ? " " + g.Tier : "");
                }
            }

            _dashCooldown.fillAmount = _player.DashCooldownFraction;
        }
    }
}
