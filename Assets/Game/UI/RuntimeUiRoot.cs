using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One programmatic placeholder Canvas shared by every M7 page and presentation
    /// overlay. Text displays localization keys until M8 connects Localization.
    /// </summary>
    public sealed class RuntimeUiRoot : MonoBehaviour, IGameFlowView
    {
        private readonly StringBuilder builder = new StringBuilder(256);
        private Canvas canvas;
        private Text pageText;
        private Font runtimeFont;

        public Canvas SharedCanvas => canvas;
        public UiPageId CurrentPage { get; private set; }
        public int RenderedOptionCount { get; private set; }
        public int RenderedSelectedIndex { get; private set; }

        public void Initialize()
        {
            if (canvas != null) return;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("M7PlaceholderPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(transform, false);
            var rect = (RectTransform)panelObject.transform;
            rect.anchorMin = new Vector2(0.05f, 0.05f);
            rect.anchorMax = new Vector2(0.48f, 0.55f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.88f);

            var textObject = new GameObject("LocalizedKeyPreview", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 24f);
            textRect.offsetMax = new Vector2(-24f, -24f);
            pageText = textObject.GetComponent<Text>();
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pageText.font = runtimeFont;
            pageText.fontSize = 20;
            pageText.alignment = TextAnchor.UpperLeft;
            pageText.color = Color.white;
            pageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            pageText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        public void Show(UiPageViewModel model)
        {
            if (canvas == null) Initialize();
            CurrentPage = model.Page;
            RenderedOptionCount = model.OptionCount;
            RenderedSelectedIndex = model.SelectedIndex;
            builder.Clear();
            builder.Append(model.TitleKey);
            for (var index = 0; index < model.OptionCount; index++)
            {
                builder.Append('\n');
                builder.Append(index == model.SelectedIndex ? "> " : "  ");
                builder.Append(model.GetOptionKey(index));
            }

            pageText.text = builder.ToString();
        }

        private void OnDestroy()
        {
            runtimeFont = null;
        }
    }
}
