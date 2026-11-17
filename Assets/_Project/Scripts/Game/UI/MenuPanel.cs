using System;
using System.Collections.Generic;
using System.Text;
using HandSeals.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HandSeals.Game
{
    /// <summary>
    /// The main menu: training, "play all" and a scrollable list of techniques with their seals and best times.
    /// </summary>
    internal sealed class MenuPanel
    {
        private const float ItemHeight = 176f;
        private const float ItemGap = 18f;

        private readonly GameObject _root;
        private readonly Text _hint;
        private readonly RectTransform _content;
        private readonly List<Text> _itemLabels = new List<Text>();
        private readonly StringBuilder _builder = new StringBuilder(160);

        public MenuPanel(Transform parent)
        {
            RectTransform root = UiFactory.CreateRect("Menu", parent, Vector2.zero, Vector2.one);
            _root = root.gameObject;

            Text title = UiFactory.AddText(
                UiFactory.CreateRect("Title", root, new Vector2(0.06f, 0.87f), new Vector2(0.94f, 0.97f)),
                104, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            title.text = "HandSealAI";
            UiFactory.AddOutline(title);

            _hint = UiFactory.AddText(
                UiFactory.CreateRect("Hint", root, new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.87f)),
                36, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f));

            UiFactory.AddButton(
                UiFactory.CreateRect("TrainingButton", root, new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.785f)),
                "<b>Training</b>\n<size=34>learn the seals one by one, no timer</size>",
                56, TextAnchor.MiddleCenter, UiFactory.AccentButtonColor,
                () => TrainingClicked?.Invoke());

            UiFactory.AddButton(
                UiFactory.CreateRect("PlayAllButton", root, new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.685f)),
                "<b>Play all techniques</b>\n<size=34>one after another, with combo</size>",
                56, TextAnchor.MiddleCenter, UiFactory.ButtonColor,
                () => PlayAllClicked?.Invoke());

            Text listTitle = UiFactory.AddText(
                UiFactory.CreateRect("ListTitle", root, new Vector2(0.06f, 0.545f), new Vector2(0.94f, 0.59f)),
                38, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            listTitle.text = "Or pick one technique to repeat:";

            // Scrollable list. The viewport clips the content and catches drags that start between the items.
            RectTransform scrollRect = UiFactory.CreateRect("Techniques", root, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.54f));
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = UiFactory.CreateRect("Viewport", scrollRect, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            _content = UiFactory.CreateTopRow("Content", viewport, 0f, 0f);

            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            UiFactory.AddButton(
                UiFactory.CreateRect("SettingsButton", root, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.1f)),
                "Settings", 46, TextAnchor.MiddleCenter, UiFactory.ButtonColor,
                () => SettingsClicked?.Invoke());
        }

        public event Action TrainingClicked;

        public event Action PlayAllClicked;

        public event Action SettingsClicked;

        /// <summary>Raised with the index of the tapped technique.</summary>
        public event Action<int> TechniqueClicked;

        public void SetVisible(bool visible)
        {
            _root.SetActive(visible);
        }

        /// <summary>Rebuilds the labels; call when the menu opens, because best times change while playing.</summary>
        public void Refresh(GameController game)
        {
            IReadOnlyList<Jutsu> techniques = game.Techniques;

            _hint.text = game.Error != null
                ? "<color=#F56B6B>Techniques could not be loaded:\n" + game.Error + "</color>"
                : "Stand the phone upright and step back until\nyour hands and chest are in the picture.";

            if (_itemLabels.Count != techniques.Count)
            {
                BuildItems(techniques.Count);
            }

            for (int i = 0; i < techniques.Count; i++)
            {
                Jutsu jutsu = techniques[i];

                _builder.Length = 0;
                _builder.Append("<b>").Append(jutsu.Name).Append("</b>\n<size=34><color=#C8C8C8>");
                for (int s = 0; s < jutsu.Sequence.Count; s++)
                {
                    if (s > 0)
                    {
                        _builder.Append(" › ");
                    }

                    _builder.Append(jutsu.Sequence[s].ToString());
                }

                _builder.Append("</color></size>\n<size=30><color=#9E9E9E>")
                    .Append(UiFormat.Seconds(jutsu.TimeLimitSeconds));

                if (game.TryGetBestTime(jutsu, out double best))
                {
                    _builder.Append("  ·  best ").Append(UiFormat.Seconds(best));
                }

                _builder.Append("</color></size>");
                _itemLabels[i].text = _builder.ToString();
            }
        }

        private void BuildItems(int count)
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            }

            _itemLabels.Clear();

            for (int i = 0; i < count; i++)
            {
                int index = i;
                RectTransform item = UiFactory.CreateTopRow("Technique" + i, _content, i * (ItemHeight + ItemGap), ItemHeight);
                Text label = UiFactory.AddButton(
                    item, string.Empty, 50, TextAnchor.MiddleLeft, UiFactory.ButtonColor,
                    () => TechniqueClicked?.Invoke(index));
                _itemLabels.Add(label);
            }

            float height = count > 0 ? count * (ItemHeight + ItemGap) - ItemGap : 0f;
            _content.sizeDelta = new Vector2(0f, height);
        }
    }
}
