using System;
using HandSeals.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HandSeals.Game
{
    /// <summary>
    /// The settings screen: steppers for the recognition sensitivity and hold time, switches for sound,
    /// GPU and the model-input debug view. Every change is applied and saved at once.
    /// </summary>
    internal sealed class SettingsPanel
    {
        private const float RowHeight = 176f;
        private const float RowGap = 20f;

        private static readonly Color RowColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color OnColor = new Color(0.43f, 0.91f, 0.63f);
        private static readonly Color OffColor = new Color(0.62f, 0.62f, 0.62f);

        private readonly GameObject _root;
        private readonly Text _confidence;
        private readonly Text _hold;
        private readonly Text _sound;
        private readonly Text _gpu;
        private readonly Text _modelInput;
        private GameSettings _working = new GameSettings();

        public SettingsPanel(Transform parent)
        {
            RectTransform root = UiFactory.CreateRect("Settings", parent, Vector2.zero, Vector2.one);
            _root = root.gameObject;

            Text title = UiFactory.AddText(
                UiFactory.CreateRect("Title", root, new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f)),
                88, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            title.text = "Settings";
            UiFactory.AddOutline(title);

            RectTransform rows = UiFactory.CreateRect("Rows", root, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.86f));

            _confidence = AddStepperRow(
                rows, 0, "Confidence needed", "lower: seals count sooner, but so do mix-ups",
                () => Change(s => s.StepConfirmScore(-1)), () => Change(s => s.StepConfirmScore(1)));

            _hold = AddStepperRow(
                rows, 1, "Hold time", "how long a seal must be held to count",
                () => Change(s => s.StepHoldSeconds(-1)), () => Change(s => s.StepHoldSeconds(1)));

            _sound = AddToggleRow(rows, 2, "Sound", "effects for right and wrong seals", () => Change(s => s.SoundOn = !s.SoundOn));

            _gpu = AddToggleRow(
                rows, 3, "Run the model on the GPU", "turn off if recognition looks wrong",
                () => Change(s => s.UseGpu = !s.UseGpu));

            _modelInput = AddToggleRow(
                rows, 4, "Show model input", "debug: the picture the model receives",
                () => Change(s => s.ShowModelInput = !s.ShowModelInput));

            UiFactory.AddButton(
                UiFactory.CreateRect("ResetButton", root, new Vector2(0.05f, 0.1f), new Vector2(0.48f, 0.17f)),
                "Reset to defaults", 44, TextAnchor.MiddleCenter, UiFactory.ButtonColor,
                () => Change(s => Reset(s)));

            UiFactory.AddButton(
                UiFactory.CreateRect("BackButton", root, new Vector2(0.52f, 0.1f), new Vector2(0.95f, 0.17f)),
                "<b>‹ Back</b>", 48, TextAnchor.MiddleCenter, UiFactory.AccentButtonColor,
                () => BackClicked?.Invoke());
        }

        /// <summary>Raised with a copy of the new settings after every change.</summary>
        public event Action<GameSettings> Changed;

        public event Action BackClicked;

        public void SetVisible(bool visible)
        {
            _root.SetActive(visible);
        }

        /// <summary>Shows the given settings; the panel edits its own copy.</summary>
        public void Show(GameSettings current)
        {
            _working = current.Clone();
            Refresh();
        }

        private void Change(Action<GameSettings> change)
        {
            change(_working);
            _working.Normalize();
            Refresh();
            Changed?.Invoke(_working.Clone());
        }

        private static void Reset(GameSettings settings)
        {
            var defaults = new GameSettings();
            settings.ConfirmScore = defaults.ConfirmScore;
            settings.HoldSeconds = defaults.HoldSeconds;
            settings.SoundOn = defaults.SoundOn;
            settings.UseGpu = defaults.UseGpu;
            settings.ShowModelInput = defaults.ShowModelInput;
        }

        private void Refresh()
        {
            _confidence.text = UiFormat.Percent(_working.ConfirmScore);
            _hold.text = UiFormat.Seconds(_working.HoldSeconds);
            SetSwitch(_sound, _working.SoundOn);
            SetSwitch(_gpu, _working.UseGpu);
            SetSwitch(_modelInput, _working.ShowModelInput);
        }

        private static void SetSwitch(Text label, bool on)
        {
            label.text = on ? "<b>On</b>" : "Off";
            label.color = on ? OnColor : OffColor;
        }

        private static RectTransform AddRow(RectTransform rows, int index, string name, string description)
        {
            RectTransform row = UiFactory.CreateTopRow("Row" + index, rows, index * (RowHeight + RowGap), RowHeight);
            UiFactory.AddImage(row, RowColor);

            Text title = UiFactory.AddText(
                UiFactory.CreateRect("Name", row, new Vector2(0.04f, 0.48f), new Vector2(0.62f, 0.94f)),
                44, TextAnchor.LowerLeft, Color.white, FontStyle.Bold);
            title.text = name;

            Text note = UiFactory.AddText(
                UiFactory.CreateRect("Description", row, new Vector2(0.04f, 0.06f), new Vector2(0.62f, 0.46f)),
                30, TextAnchor.UpperLeft, OffColor);
            note.text = description;

            return row;
        }

        // Returns the label that shows the value.
        private static Text AddStepperRow(
            RectTransform rows, int index, string name, string description, Action decrease, Action increase)
        {
            RectTransform row = AddRow(rows, index, name, description);

            UiFactory.AddButton(
                UiFactory.CreateRect("Minus", row, new Vector2(0.63f, 0.2f), new Vector2(0.75f, 0.8f)),
                "–", 64, TextAnchor.MiddleCenter, UiFactory.ButtonColor, () => decrease());

            Text value = UiFactory.AddText(
                UiFactory.CreateRect("Value", row, new Vector2(0.75f, 0.2f), new Vector2(0.86f, 0.8f)),
                44, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

            UiFactory.AddButton(
                UiFactory.CreateRect("Plus", row, new Vector2(0.86f, 0.2f), new Vector2(0.98f, 0.8f)),
                "+", 64, TextAnchor.MiddleCenter, UiFactory.ButtonColor, () => increase());

            return value;
        }

        private static Text AddToggleRow(RectTransform rows, int index, string name, string description, Action toggle)
        {
            RectTransform row = AddRow(rows, index, name, description);

            return UiFactory.AddButton(
                UiFactory.CreateRect("Switch", row, new Vector2(0.7f, 0.2f), new Vector2(0.98f, 0.8f)),
                "Off", 48, TextAnchor.MiddleCenter, UiFactory.ButtonColor, () => toggle());
        }
    }
}
