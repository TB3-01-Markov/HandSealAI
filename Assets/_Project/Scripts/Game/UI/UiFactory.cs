using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HandSeals.Game
{
    /// <summary>Small helpers for building the uGUI hierarchy from code.</summary>
    internal static class UiFactory
    {
        public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color AccentButtonColor = new Color(0.18f, 0.55f, 0.36f, 0.95f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _font;
            }
        }

        /// <summary>Creates an empty UI object stretched over the given anchor rectangle of its parent.</summary>
        public static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Creates a UI object of a fixed size positioned around its parent's centre.</summary>
        public static RectTransform CreateCentered(string name, Transform parent, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Creates a row of a fixed height pinned to the top of its parent, <paramref name="offsetFromTop"/> below it.</summary>
        public static RectTransform CreateTopRow(string name, Transform parent, float offsetFromTop, float height)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -offsetFromTop);
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

        public static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text AddText(RectTransform rect, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Adds a dark outline so that text stays readable on top of the camera image.</summary>
        public static void AddOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        /// <summary>Turns a rectangle into a tappable button with a text label and returns the label.</summary>
        public static Text AddButton(
            RectTransform rect,
            string label,
            int fontSize,
            TextAnchor alignment,
            Color background,
            UnityAction onClick)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            RectTransform labelRect = CreateRect("Label", rect, Vector2.zero, Vector2.one);
            labelRect.offsetMin = new Vector2(36f, 8f);
            labelRect.offsetMax = new Vector2(-36f, -8f);

            Text text = AddText(labelRect, fontSize, alignment, Color.white);
            text.text = label;
            return text;
        }
    }
}
