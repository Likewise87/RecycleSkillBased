using System;
using UnityEngine;
using Verse;

namespace RecycleSkillBased
{
    public enum SliderFormat { Fixed0, Fixed1, Fixed2, Fixed3, Multiplier, Percent, PercentDecimal, PercentInt }

    public class RecycleSkillBasedMod : Mod
    {
        public static Settings Settings;

        private Vector2 scrollPos = Vector2.zero;
        private static readonly Color SectionHeaderColor = new Color(0.55f, 0.85f, 1f);

        private bool yieldExpanded = true;
        private bool xpExpanded = true;
        private bool speedExpanded = true;
        private bool optionsExpanded = true;

        public RecycleSkillBasedMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();
        }

        public override string SettingsCategory() => "Recycle Skill Based";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float contentWidth = inRect.width - 24f;
            float viewHeight = EstimateScrollHeight();
            Rect viewRect = new Rect(0f, 0f, contentWidth, viewHeight);

            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            var l = new Listing_Standard();
            l.Begin(viewRect);

            DrawMenuTopBar(l, "Restore defaults", () => Settings.ResetToDefaults(),
                () => { yieldExpanded = xpExpanded = speedExpanded = optionsExpanded = true; },
                () => { yieldExpanded = xpExpanded = speedExpanded = optionsExpanded = false; });

            if (DrawCollapsibleHeader(l, "Yield", ref yieldExpanded, SectionHeaderColor))
            {
                Settings.ReturnAtLevel0 = Mathf.RoundToInt(LabeledSlider(l,
                    "Materials returned at crafting skill 0",
                    Settings.ReturnAtLevel0, 0f, 100f,
                    "Percent of original cost returned at Crafting skill 0.\nFinal yield is linearly interpolated up to the level 20 value.",
                    1f, SliderFormat.PercentInt, 10f));
                if (Settings.ReturnAtLevel0 > Settings.ReturnAtLevel20)
                    Settings.ReturnAtLevel20 = Settings.ReturnAtLevel0;

                Settings.ReturnAtLevel20 = Mathf.RoundToInt(LabeledSlider(l,
                    "Materials returned at crafting skill 20",
                    Settings.ReturnAtLevel20, 0f, 100f,
                    "Percent of original cost returned at Crafting skill 20.\nFinal yield is linearly interpolated down to the level 0 value.",
                    1f, SliderFormat.PercentInt, 75f));
                if (Settings.ReturnAtLevel20 < Settings.ReturnAtLevel0)
                    Settings.ReturnAtLevel0 = Settings.ReturnAtLevel20;

                Settings.TaintedPenaltyPercent = Mathf.RoundToInt(LabeledSlider(l,
                    "Additional penalty for tainted apparel",
                    Settings.TaintedPenaltyPercent, 0f, 100f,
                    "Extra reduction applied only to apparel marked as tainted (worn by corpse).\n0% = no penalty.\nApplied after the skill-based return, before optional HP scaling.",
                    1f, SliderFormat.PercentInt, 30f));
            }

            if (DrawCollapsibleHeader(l, "XP", ref xpExpanded, SectionHeaderColor))
            {
                // Do not pass fine roundTo into HorizontalSlider (0.001 causes idle sound spam via float !=).
                float xp = LabeledSlider(l,
                    "XP per tick while recycling",
                    Settings.XpPerTick, 0f, 3f,
                    "Crafting XP gained each game tick while working (60 ticks = 1 second).\nTotal XP per job scales with duration.",
                    -1f, SliderFormat.Fixed2, 1.0f);
                float snapped = Mathf.Clamp(Mathf.Round(xp * 100f) / 100f, 0f, 3f);
                if (Mathf.Abs(snapped - Settings.XpPerTick) >= 0.005f)
                    Settings.XpPerTick = snapped;
            }

            if (DrawCollapsibleHeader(l, "Speed", ref speedExpanded, SectionHeaderColor))
            {
                Settings.SpeedMultLevel0 = LabeledSlider(l,
                    "Recycling speed at crafting level 0",
                    Settings.SpeedMultLevel0, 0.01f, 4f,
                    "Affects how fast recycling completes.\nHigher = faster (less total work).",
                    0.01f, SliderFormat.Percent, 0.75f);
                Settings.SpeedMultLevel10 = LabeledSlider(l,
                    "Recycling speed at crafting level 10",
                    Settings.SpeedMultLevel10, 0.01f, 4f,
                    "Affects how fast recycling completes.\nHigher = faster (less total work).",
                    0.01f, SliderFormat.Percent, 1.00f);
                Settings.SpeedMultLevel20 = LabeledSlider(l,
                    "Recycling speed at crafting level 20",
                    Settings.SpeedMultLevel20, 0.01f, 4f,
                    "Affects how fast recycling completes.\nHigher = faster (less total work).",
                    0.01f, SliderFormat.Percent, 2.00f);

                if (Settings.SpeedMultLevel10 < Settings.SpeedMultLevel0)
                    Settings.SpeedMultLevel10 = Settings.SpeedMultLevel0;
                if (Settings.SpeedMultLevel20 < Settings.SpeedMultLevel10)
                    Settings.SpeedMultLevel20 = Settings.SpeedMultLevel10;
            }

            if (DrawCollapsibleHeader(l, "Options", ref optionsExpanded, SectionHeaderColor))
            {
                DrawCheckbox(l, "Allow returning industrial components", ref Settings.AllowIndustrialComponents,
                    "If disabled, Component (industrial) will not be returned by recycling.", defaultValue: true);
                DrawCheckbox(l, "Allow returning advanced components", ref Settings.AllowAdvancedComponents,
                    "If disabled, Advanced component (spacer) will not be returned by recycling.", defaultValue: true);
                DrawCheckbox(l, "Forbid recycling items on outfit/wardrobe stands", ref Settings.ForbidOutfitStandItems,
                    "When enabled, items that are on outfit/wardrobe/clothes stands will be ignored by recycling bills.", defaultValue: true);
                DrawCheckbox(l, "Health points affect returned materials", ref Settings.UseHPScaling,
                    "If enabled, the yield is multiplied by the item's current HP% after tainted penalty.\nExample: 85% HP → 0.85× materials.",
                    defaultValue: true);

                l.Gap(6f);
                string formula = "Final yield = BaseCost × SkillYield × (1 - TaintedPenalty)";
                if (Settings.UseHPScaling) formula += " × HP%";
                l.Label(formula,
                    tooltip: "BaseCost = original recipe cost list (incl. stuff where applicable)\n" +
                             "SkillYield = linear interpolation between level-0% and level-20%\n" +
                             "TaintedPenalty applies only to apparel with 'worn by corpse'\n" +
                             (Settings.UseHPScaling ? "HP% = current hit points / max hit points\n" : ""));
            }

            l.End();
            Widgets.EndScrollView();
        }

        private float EstimateScrollHeight()
        {
            float h = 80f;
            if (yieldExpanded) h += 140f;
            if (xpExpanded) h += 60f;
            if (speedExpanded) h += 130f;
            if (optionsExpanded) h += 180f;
            h += 4 * 50f;
            return Mathf.Max(h, 400f);
        }

        private static bool DrawCollapsibleHeader(Listing_Standard l, string label, ref bool expanded, Color? color = null, string tip = null)
        {
            l.Gap(12f);
            Rect r = l.GetRect(26f);
            Widgets.DrawHighlightIfMouseover(r);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r)) expanded = !expanded;
            Color c = color ?? Color.white;
            string arrow = expanded ? "▼" : "▶";
            Widgets.Label(r, $"<b><color=#{ColorUtility.ToHtmlStringRGBA(c)}>{arrow}  {label}</color></b>");
            if (expanded) { l.GapLine(6f); l.Gap(4f); }
            else l.Gap(4f);
            return expanded;
        }

        private static void DrawMenuTopBar(Listing_Standard l, string resetLabel, Action onReset, Action expandAll, Action collapseAll)
        {
            if (onReset == null) return;
            l.Gap(4f);
            Rect row = l.GetRect(30f);
            float sideBtnW = 110f;
            float rightPairW = sideBtnW * 2f + 8f;
            float resetW = Mathf.Min(250f, row.width - rightPairW - 24f);
            if (resetW < 120f) resetW = Mathf.Max(100f, row.width * 0.35f);
            Rect resetRect = new Rect(row.x, row.y, resetW, row.height);
            Rect collapseRect = new Rect(row.xMax - sideBtnW, row.y, sideBtnW, row.height);
            Rect expandRect = new Rect(collapseRect.x - 8f - sideBtnW, row.y, sideBtnW, row.height);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(resetRect, resetLabel)) onReset();
            GUI.color = prev;
            if (expandAll != null && Widgets.ButtonText(expandRect, "Expand all")) expandAll();
            if (collapseAll != null && Widgets.ButtonText(collapseRect, "Collapse all")) collapseAll();
            l.Gap(6f);
        }

        private static float LabeledSlider(Listing_Standard l, string label, float val, float min, float max, string tooltip = null, float step = 0.1f, SliderFormat format = SliderFormat.Fixed1, float? defaultValue = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultValue, format));
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {FormatValue(val, format).Colorize(Color.cyan)}");
            return Widgets.HorizontalSlider(r.RightPart(0.5f), val, min, max, false, null, null, null, step);
        }

        private static void DrawCheckbox(Listing_Standard l, string label, ref bool value, string tooltip = null, bool? defaultValue = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultValue));
            Widgets.CheckboxLabeled(r, label, ref value);
        }

        private static string TooltipWithDefault(string tooltip, float? defaultValue, SliderFormat format = SliderFormat.Fixed1)
        {
            if (!defaultValue.HasValue) return tooltip;
            return AppendDefault(tooltip, FormatValue(defaultValue.Value, format));
        }

        private static string TooltipWithDefault(string tooltip, bool? defaultValue)
        {
            if (!defaultValue.HasValue) return tooltip;
            return AppendDefault(tooltip, defaultValue.Value ? "On" : "Off");
        }

        private static string AppendDefault(string tooltip, string def)
        {
            string line = $"Default: {def}";
            return tooltip.NullOrEmpty() ? line : tooltip + "\n" + line;
        }

        private static string FormatValue(float val, SliderFormat format) => format switch
        {
            SliderFormat.Fixed0 => val.ToString("F0"),
            SliderFormat.Fixed1 => val.ToString("F1"),
            SliderFormat.Fixed2 => val.ToString("F2"),
            SliderFormat.Fixed3 => val.ToString("0.###"),
            SliderFormat.Multiplier => val.ToString("F2") + "x",
            SliderFormat.Percent => (val * 100f).ToString("F0") + " %",
            SliderFormat.PercentDecimal => (val * 100f).ToString("F1") + " %",
            SliderFormat.PercentInt => val.ToString("F0") + " %",
            _ => val.ToString()
        };
    }
}
