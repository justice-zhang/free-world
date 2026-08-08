using System;
using System.Globalization;
using System.Text;
using Game.Application;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Single procedural placeholder Canvas with separated page and HUD layers.</summary>
    public sealed class QinglanRuntimeUiRoot : MonoBehaviour, IQinglanDemoView
    {
        private static readonly string[] RuntimeFontCandidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial Unicode MS", "Arial"
        };
        private readonly StringBuilder pageBuilder = new StringBuilder(8192);
        private readonly StringBuilder hudBuilder = new StringBuilder(4096);
        private Canvas canvas;
        private CanvasScaler scaler;
        private Image pagePanel;
        private Text pageText;
        private Text hudText;
        private Text dangerText;
        private Font runtimeFont;
        private ILocalizationService localization;
        private Func<string, string> contentNameResolver;
        private ColorVisionMode lastColorVision = (ColorVisionMode)255;
        private float lastFontScale = -1f;

        public Canvas SharedCanvas => canvas;
        public QinglanUiPageId CurrentPage { get; private set; }
        public int RenderedOptionCount { get; private set; }
        public int RenderedSelectedIndex { get; private set; }
        public int HudRefreshCount { get; private set; }
        public string RenderedPageText => pageText == null ? string.Empty : pageText.text;
        public string RenderedHudText => hudText == null ? string.Empty : hudText.text;

        public void Initialize(
            ILocalizationService localizationService,
            Func<string, string> resolveContentNameKey)
        {
            if (canvas != null) return;
            localization = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            contentNameResolver = resolveContentNameKey ?? throw new ArgumentNullException(nameof(resolveContentNameKey));
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            runtimeFont = Font.CreateDynamicFontFromOSFont(RuntimeFontCandidates, 24);
            if (runtimeFont == null) runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            pagePanel = CreatePanel("Qinglan_PageLayer", new Vector2(0.04f, 0.06f), new Vector2(0.58f, 0.94f));
            pageText = CreateText(pagePanel.transform, "Qinglan_PageText", 24, TextAnchor.UpperLeft,
                new Vector2(34f, 30f), new Vector2(-34f, -30f));
            var hudPanel = CreatePanel("Qinglan_HudLayer", new Vector2(0.62f, 0.58f), new Vector2(0.97f, 0.94f));
            hudText = CreateText(hudPanel.transform, "Qinglan_HudText", 20, TextAnchor.UpperLeft,
                new Vector2(24f, 20f), new Vector2(-24f, -20f));
            var dangerPanel = CreatePanel("Qinglan_DangerLayer", new Vector2(0.62f, 0.06f), new Vector2(0.97f, 0.54f));
            dangerText = CreateText(dangerPanel.transform, "Qinglan_DangerText", 21, TextAnchor.UpperLeft,
                new Vector2(24f, 20f), new Vector2(-24f, -20f));
            RefreshDangerLegend();
        }

        public void ShowPage(QinglanPageViewModel page)
        {
            if (canvas == null) throw new InvalidOperationException("UI root is not initialized.");
            CurrentPage = page.Page;
            RenderedOptionCount = page.OptionCount;
            RenderedSelectedIndex = page.SelectedIndex;
            pageBuilder.Clear();
            AppendKey(pageBuilder, page.TitleKey);
            if (!string.IsNullOrEmpty(page.SubtitleKey))
            {
                pageBuilder.Append("\n\n");
                AppendKey(pageBuilder, page.SubtitleKey);
            }
            if (!string.IsNullOrEmpty(page.StatusKey))
            {
                pageBuilder.Append("\n\n◆ ");
                AppendKey(pageBuilder, page.StatusKey);
                if (!string.IsNullOrEmpty(page.StatusValue)) pageBuilder.Append(": ").Append(page.StatusValue);
            }
            for (var index = 0; index < page.OptionCount; index++)
            {
                var option = page.GetOptionAt(index);
                pageBuilder.Append("\n\n");
                pageBuilder.Append(index == page.SelectedIndex ? "◆ " : option.Enabled ? "◇ " : "× ");
                AppendKey(pageBuilder, option.LabelKey);
                if (!string.IsNullOrEmpty(option.ValueText))
                {
                    pageBuilder.Append("  [");
                    AppendValue(pageBuilder, option.ValueText);
                    pageBuilder.Append(']');
                }
                if (!string.IsNullOrEmpty(option.DescriptionKey))
                {
                    pageBuilder.Append("\n   ");
                    AppendKey(pageBuilder, option.DescriptionKey);
                }
                AppendCardMetadata(option);
            }
            pageText.text = pageBuilder.ToString();
        }

        public void ShowHud(RunUiSnapshot snapshot)
        {
            if (canvas == null || snapshot == null) return;
            HudRefreshCount++;
            hudBuilder.Clear();
            AppendKey(hudBuilder, "ui.qinglan.hud.vitals");
            hudBuilder.Append("\n♥ ").Append(Round(snapshot.Health)).Append('/').Append(Round(snapshot.MaximumHealth));
            hudBuilder.Append("   ◇ ").Append(Round(snapshot.Shield)).Append('/').Append(Round(snapshot.MaximumShield));
            hudBuilder.Append("\nLv.").Append(snapshot.Level).Append("  XP ").Append(Round(snapshot.Experience)).Append('/').Append(Round(snapshot.RequiredExperience));
            hudBuilder.Append("\n\n");
            AppendKey(hudBuilder, "ui.qinglan.hud.run");
            hudBuilder.Append("\n◷ ").Append(FormatTime(snapshot.DurationSeconds));
            hudBuilder.Append("   ");
            AppendKey(hudBuilder, "ui.qinglan.hud.windride");
            hudBuilder.Append(' ').Append(snapshot.MechanicTier + 1).Append("/4  ≫ ").Append(Round(snapshot.MechanicValue));
            if (snapshot.HasBoss)
            {
                hudBuilder.Append("\n\n▲ ").Append(ResolveContent(snapshot.BossId));
                hudBuilder.Append("  ").Append(snapshot.BossPhase + 1).Append('/').Append(snapshot.BossPhaseCount);
                hudBuilder.Append("\n").Append(Round(snapshot.BossHealth)).Append('/').Append(Round(snapshot.BossMaximumHealth));
            }
            hudBuilder.Append("\n\n");
            AppendKey(hudBuilder, "ui.qinglan.hud.build");
            for (var index = 0; index < snapshot.BuildCount; index++)
            {
                var item = snapshot.GetBuildAt(index);
                hudBuilder.Append("\n").Append(BuildGlyph(item.Kind)).Append(' ').Append(ResolveContent(item.ContentId));
                hudBuilder.Append("  Lv.").Append(item.Level).Append('/').Append(item.MaximumLevel);
            }
            hudBuilder.Append("\n\n");
            AppendKey(hudBuilder, "ui.qinglan.hud.map");
            for (var index = 0; index < snapshot.MapCount; index++)
            {
                var item = snapshot.GetMapAt(index);
                hudBuilder.Append("\n").Append(MapGlyph(item.Kind)).Append(' ').Append(ResolveContent(item.ContentId));
                hudBuilder.Append("  ").Append(Round(item.Progress * 100f)).Append('%');
            }
            hudText.text = hudBuilder.ToString();
        }

        public void ApplyAccessibility(AccessibilitySettings settings)
        {
            if (settings == null || canvas == null) return;
            if (Math.Abs(lastFontScale - settings.FontScale) > 0.001f)
            {
                lastFontScale = settings.FontScale;
                pageText.fontSize = Mathf.RoundToInt(24f * settings.FontScale);
                hudText.fontSize = Mathf.RoundToInt(20f * settings.FontScale);
                dangerText.fontSize = Mathf.RoundToInt(21f * settings.FontScale);
            }
            if (lastColorVision == settings.ColorVision) return;
            lastColorVision = settings.ColorVision;
            pagePanel.color = PanelColor(settings.ColorVision);
            dangerText.color = DangerColor(settings.ColorVision);
            RefreshDangerLegend();
        }

        public bool SupportsCharacter(char character)
        {
            if (runtimeFont == null) return false;
            runtimeFont.RequestCharactersInTexture(character.ToString(), pageText == null ? 24 : pageText.fontSize);
            return runtimeFont.HasCharacter(character);
        }

        private void AppendCardMetadata(QinglanUiOption option)
        {
            if (string.IsNullOrEmpty(option.TagKey) && string.IsNullOrEmpty(option.RelationKey) &&
                string.IsNullOrEmpty(option.EligibilityKey)) return;
            pageBuilder.Append("\n   ");
            if (!string.IsNullOrEmpty(option.TagKey))
            {
                pageBuilder.Append('[');
                AppendKey(pageBuilder, option.TagKey);
                pageBuilder.Append("] ");
            }
            if (!string.IsNullOrEmpty(option.RelationKey)) AppendKey(pageBuilder, option.RelationKey);
            if (!string.IsNullOrEmpty(option.EligibilityKey))
            {
                pageBuilder.Append(" · ");
                AppendKey(pageBuilder, option.EligibilityKey);
            }
        }

        private void RefreshDangerLegend()
        {
            // Shape and direction remain readable when hue cannot be distinguished.
            dangerText.text = "▲  ▶  ◆  " + Resolve("ui.qinglan.accessibility.danger_legend");
        }

        private Image CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(transform, false);
            var rect = (RectTransform)panelObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = panelObject.GetComponent<Image>();
            image.color = new Color(0.035f, 0.075f, 0.085f, 0.92f);
            return image;
        }

        private Text CreateText(
            Transform parent,
            string name,
            int fontSize,
            TextAnchor alignment,
            Vector2 minimumOffset,
            Vector2 maximumOffset)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimumOffset;
            rect.offsetMax = maximumOffset;
            var text = textObject.GetComponent<Text>();
            text.font = runtimeFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.95f, 0.94f, 0.84f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void AppendKey(StringBuilder builder, string key)
        {
            if (!string.IsNullOrEmpty(key)) builder.Append(Resolve(key));
        }

        private void AppendValue(StringBuilder builder, string value)
        {
            if (value.StartsWith("ui.", StringComparison.Ordinal) || value.StartsWith("content.", StringComparison.Ordinal))
                builder.Append(Resolve(value));
            else builder.Append(value);
        }

        private string Resolve(string key) => localization.Resolve(key);

        private string ResolveContent(string id)
        {
            var key = contentNameResolver(id);
            return string.IsNullOrEmpty(key) ? Resolve("ui.qinglan.content.unknown") : Resolve(key);
        }

        private static string Round(float value) =>
            Math.Round(value, 1).ToString(CultureInfo.InvariantCulture);

        private static string FormatTime(double seconds)
        {
            var total = Math.Max(0, (int)seconds);
            return (total / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                   (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static string BuildGlyph(byte kind) => kind == 1 ? "⚔" : kind == 2 ? "✦" : kind == 3 ? "⬡" : "✧";
        private static string MapGlyph(byte kind) => kind == 1 ? "◎" : kind == 2 ? "△" : "◇";

        private static Color PanelColor(ColorVisionMode mode)
        {
            switch (mode)
            {
                case ColorVisionMode.Protanopia: return new Color(0.035f, 0.07f, 0.12f, 0.94f);
                case ColorVisionMode.Deuteranopia: return new Color(0.06f, 0.055f, 0.12f, 0.94f);
                case ColorVisionMode.Tritanopia: return new Color(0.09f, 0.05f, 0.07f, 0.94f);
                case ColorVisionMode.HighContrast: return new Color(0f, 0f, 0f, 0.98f);
                default: return new Color(0.035f, 0.075f, 0.085f, 0.92f);
            }
        }

        private static Color DangerColor(ColorVisionMode mode) =>
            mode == ColorVisionMode.HighContrast ? Color.white : new Color(1f, 0.72f, 0.32f, 1f);
    }
}
