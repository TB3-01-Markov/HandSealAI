using System.Text;
using HandSeals.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HandSeals.Game
{
    /// <summary>
    /// The whole screen: mirrored camera preview with the detection box, the current jutsu and its sequence,
    /// the recognised seal with a hold bar, and a status line. Built from code, so no prefabs are needed.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float FeedbackSeconds = 0.5f;
        private const float BoxThickness = 6f;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color GoodColor = new Color(0.43f, 0.91f, 0.63f);
        private static readonly Color BadColor = new Color(0.96f, 0.42f, 0.42f);
        private static readonly Color MutedColor = new Color(0.62f, 0.62f, 0.62f);

        private CameraFeed _camera;
        private RecognitionController _recognition;
        private GameController _game;

        private RectTransform _previewArea;
        private RectTransform _mirrorRoot;
        private RawImage _preview;
        private RectTransform _box;
        private Image[] _boxEdges;
        private Image _topPanel;
        private Text _title;
        private Text _timer;
        private Text _sequence;
        private Text _message;
        private Text _seal;
        private RectTransform _holdFill;
        private Image _holdFillImage;
        private Text _status;
        private RawImage _modelInput;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private float _previewScale = 1f;
        private int _sequenceSignature = -1;

        public void Initialize(CameraFeed cameraFeed, RecognitionController recognition, GameController game, bool showModelInput)
        {
            _camera = cameraFeed;
            _recognition = recognition;
            _game = game;
            _recognition.CaptureModelInput = showModelInput;

            Build(showModelInput);
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            UpdatePreview();
            UpdateRecognition();
            UpdateGame();
            UpdateStatus();
        }

        // ---------------------------------------------------------------- construction

        private void Build(bool showModelInput)
        {
            var canvasObject = new GameObject("HUD", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasObject.transform;

            UiFactory.AddImage(UiFactory.CreateRect("Background", root, Vector2.zero, Vector2.one), Color.black);

            // Camera preview. The mirror root flips everything inside it horizontally for the front camera,
            // so the detection box follows the mirrored picture without extra maths.
            _previewArea = UiFactory.CreateRect("PreviewArea", root, Vector2.zero, Vector2.one);
            _mirrorRoot = UiFactory.CreateCentered("MirrorRoot", _previewArea, Vector2.zero);

            RectTransform previewRect = UiFactory.CreateCentered("Preview", _mirrorRoot, new Vector2(100f, 100f));
            _preview = previewRect.gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false;
            _preview.enabled = false;

            _box = UiFactory.CreateCentered("DetectionBox", _mirrorRoot, new Vector2(100f, 100f));
            _boxEdges = new[]
            {
                CreateEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -BoxThickness), Vector2.zero),
                CreateEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BoxThickness)),
                CreateEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BoxThickness, 0f)),
                CreateEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-BoxThickness, 0f), Vector2.zero)
            };
            _box.gameObject.SetActive(false);

            // Top panel: jutsu name, timer and the seal sequence.
            RectTransform top = UiFactory.CreateRect("TopPanel", root, new Vector2(0f, 0.82f), Vector2.one);
            _topPanel = UiFactory.AddImage(top, PanelColor);
            _title = UiFactory.AddText(
                UiFactory.CreateRect("Title", top, new Vector2(0.04f, 0.5f), new Vector2(0.7f, 0.95f)),
                60, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            _timer = UiFactory.AddText(
                UiFactory.CreateRect("Timer", top, new Vector2(0.7f, 0.5f), new Vector2(0.96f, 0.95f)),
                68, TextAnchor.MiddleRight, Color.white, FontStyle.Bold);
            _sequence = UiFactory.AddText(
                UiFactory.CreateRect("Sequence", top, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.5f)),
                46, TextAnchor.MiddleLeft, Color.white);

            // Centre message: countdown, results, camera problems.
            _message = UiFactory.AddText(
                UiFactory.CreateRect("Message", root, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.64f)),
                88, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.AddOutline(_message);

            // Bottom panel: what the model sees right now.
            RectTransform bottom = UiFactory.CreateRect("BottomPanel", root, Vector2.zero, new Vector2(1f, 0.17f));
            UiFactory.AddImage(bottom, PanelColor);
            _seal = UiFactory.AddText(
                UiFactory.CreateRect("Seal", bottom, new Vector2(0.04f, 0.45f), new Vector2(0.96f, 0.97f)),
                84, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

            RectTransform holdBar = UiFactory.CreateRect("HoldBar", bottom, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.39f));
            UiFactory.AddImage(holdBar, new Color(1f, 1f, 1f, 0.18f));
            _holdFill = UiFactory.CreateRect("Fill", holdBar, Vector2.zero, new Vector2(0f, 1f));
            _holdFillImage = UiFactory.AddImage(_holdFill, Color.white);

            _status = UiFactory.AddText(
                UiFactory.CreateRect("Status", bottom, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.27f)),
                28, TextAnchor.MiddleCenter, MutedColor);

            if (showModelInput)
            {
                // Debug view in the top-right corner, just below the top panel.
                var modelInputObject = new GameObject("ModelInput", typeof(RectTransform));
                var modelInputRect = (RectTransform)modelInputObject.transform;
                modelInputRect.SetParent(root, false);
                modelInputRect.anchorMin = new Vector2(1f, 0.82f);
                modelInputRect.anchorMax = new Vector2(1f, 0.82f);
                modelInputRect.pivot = new Vector2(1f, 1f);
                modelInputRect.anchoredPosition = new Vector2(-16f, -16f);
                modelInputRect.sizeDelta = new Vector2(320f, 320f);
                _modelInput = modelInputObject.AddComponent<RawImage>();
                _modelInput.raycastTarget = false;
                _modelInput.enabled = false;
            }
        }

        private Image CreateEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform edge = UiFactory.CreateRect(edgeName, _box, anchorMin, anchorMax);
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            return UiFactory.AddImage(edge, Color.white);
        }

        // ---------------------------------------------------------------- per-frame updates

        private void UpdatePreview()
        {
            if (!_camera.IsRunning)
            {
                _preview.enabled = false;
                _box.gameObject.SetActive(false);
                return;
            }

            if (_preview.texture != _camera.Texture)
            {
                _preview.texture = _camera.Texture;
            }

            _preview.enabled = true;

            int angle = _camera.RotationAngle;
            bool swapAxes = angle % 180 != 0;
            float storedWidth = _camera.Width;
            float storedHeight = _camera.Height;
            float uprightWidth = swapAxes ? storedHeight : storedWidth;
            float uprightHeight = swapAxes ? storedWidth : storedHeight;

            // Fill the whole screen, cropping whatever does not fit.
            Vector2 area = _previewArea.rect.size;
            _previewScale = Mathf.Max(area.x / uprightWidth, area.y / uprightHeight);

            RectTransform previewRect = _preview.rectTransform;
            previewRect.sizeDelta = new Vector2(storedWidth * _previewScale, storedHeight * _previewScale);
            previewRect.localEulerAngles = new Vector3(0f, 0f, -angle);
            previewRect.localScale = new Vector3(1f, _camera.VerticallyMirrored ? -1f : 1f, 1f);

            // A front camera should behave like a mirror.
            _mirrorRoot.localScale = new Vector3(_camera.IsFrontFacing ? -1f : 1f, 1f, 1f);

            if (_modelInput != null && _recognition.ModelInputTexture != null)
            {
                _modelInput.texture = _recognition.ModelInputTexture;
                _modelInput.enabled = true;
            }
        }

        private void UpdateRecognition()
        {
            SealStabilizer stabilizer = _recognition.Stabilizer;
            Detection? best = _recognition.Best;
            LetterboxInfo frame = _recognition.Frame;

            bool hasBox = _camera.IsRunning && best.HasValue && frame.ImageWidth > 0;
            _box.gameObject.SetActive(hasBox);

            Color accent = stabilizer.IsConfirmed ? GoodColor : Color.white;

            if (hasBox)
            {
                BoundingBox box = best.Value.Box;
                _box.sizeDelta = new Vector2(box.Width * _previewScale, box.Height * _previewScale);
                _box.anchoredPosition = new Vector2(
                    (box.CenterX - frame.ImageWidth * 0.5f) * _previewScale,
                    (frame.ImageHeight * 0.5f - box.CenterY) * _previewScale);

                foreach (Image edge in _boxEdges)
                {
                    edge.color = accent;
                }
            }

            if (best.HasValue)
            {
                _seal.text = best.Value.Seal + "  " + Mathf.RoundToInt(best.Value.Score * 100f) + "%";
                _seal.color = best.Value.Seal.IsSeal() ? accent : MutedColor;
            }
            else
            {
                _seal.text = "show a seal";
                _seal.color = MutedColor;
            }

            float hold = (float)stabilizer.GetHoldProgress(Time.unscaledTimeAsDouble);
            _holdFill.anchorMax = new Vector2(hold, 1f);
            _holdFillImage.color = accent;
        }

        private void UpdateGame()
        {
            _topPanel.color = PanelColor;

            if (_recognition.Error != null)
            {
                ShowOnlyMessage("Model error:\n" + _recognition.Error, 44);
                return;
            }

            if (_game.Error != null)
            {
                ShowOnlyMessage("Game data error:\n" + _game.Error, 44);
                return;
            }

            switch (_camera.State)
            {
                case CameraFeedState.Starting:
                    ShowOnlyMessage("Starting the camera…", 64);
                    return;
                case CameraFeedState.PermissionDenied:
                    ShowOnlyMessage("Camera access is needed.\nAllow it in the system settings and come back.", 52);
                    return;
                case CameraFeedState.NoCamera:
                    ShowOnlyMessage("No camera found.", 64);
                    return;
            }

            GameSession session = _game.Session;
            Jutsu jutsu = session != null ? session.Current : null;
            if (jutsu == null)
            {
                ShowOnlyMessage(string.Empty, 64);
                return;
            }

            _title.text = _game.TryGetBestTime(out double bestSeconds)
                ? jutsu.Name + "\n<size=34><color=#9E9E9E>best " + bestSeconds.ToString("0.0") + " s</color></size>"
                : jutsu.Name;

            UpdateSequence(jutsu, session);

            // Brief colour feedback after each confirmed seal.
            if (Time.unscaledTimeAsDouble - _game.LastMatchTime < FeedbackSeconds)
            {
                if (_game.LastMatch == MatchResult.Wrong)
                {
                    _topPanel.color = new Color(0.55f, 0.08f, 0.08f, 0.8f);
                }
                else if (_game.LastMatch == MatchResult.Correct)
                {
                    _topPanel.color = new Color(0.06f, 0.4f, 0.2f, 0.8f);
                }
            }

            switch (session.State)
            {
                case SessionState.Countdown:
                    _timer.text = string.Empty;
                    SetMessage(Mathf.CeilToInt((float)session.CountdownLeft).ToString(), 220, Color.white);
                    break;

                case SessionState.WaitingForSeal:
                    _timer.text = session.TimeLeft.ToString("0.0") + " s";
                    _timer.color = session.TimeLeft < 3.0 ? BadColor : Color.white;
                    SetMessage(string.Empty, 88, Color.white);
                    break;

                case SessionState.Paused:
                    SetMessage("Paused", 88, Color.white);
                    break;

                case SessionState.Completed:
                    _timer.text = string.Empty;
                    SetMessage(
                        jutsu.Name + "!\n<size=56>+" + session.LastScore + "  ·  " + _game.LastTime.ToString("0.0") + " s"
                        + (_game.LastWasRecord ? "  ·  new best" : string.Empty)
                        + (session.Combo > 1 ? "\ncombo x" + session.Combo : string.Empty) + "</size>",
                        88,
                        GoodColor);
                    break;

                case SessionState.Failed:
                    _timer.text = "0.0 s";
                    SetMessage("Time's up\n<size=56>try again</size>", 88, BadColor);
                    break;

                default:
                    _timer.text = string.Empty;
                    SetMessage(string.Empty, 88, Color.white);
                    break;
            }
        }

        private void UpdateSequence(Jutsu jutsu, GameSession session)
        {
            int signature = (jutsu.Id.GetHashCode() * 397) ^ (session.Progress * 31) ^ (int)session.State;
            if (signature == _sequenceSignature)
            {
                return;
            }

            _sequenceSignature = signature;

            bool active = session.State == SessionState.WaitingForSeal || session.State == SessionState.Paused;
            int done = session.State == SessionState.Completed ? jutsu.Sequence.Count : session.Progress;

            _builder.Length = 0;
            for (int i = 0; i < jutsu.Sequence.Count; i++)
            {
                if (i > 0)
                {
                    _builder.Append("<color=#7A7A7A>  ›  </color>");
                }

                string sealName = jutsu.Sequence[i].ToString();
                if (i < done)
                {
                    _builder.Append("<color=#6EE7A0>").Append(sealName).Append("</color>");
                }
                else if (i == done && active)
                {
                    _builder.Append("<b>").Append(sealName).Append("</b>");
                }
                else
                {
                    _builder.Append("<color=#B0B0B0>").Append(sealName).Append("</color>");
                }
            }

            _sequence.text = _builder.ToString();
        }

        private void UpdateStatus()
        {
            GameSession session = _game.Session;

            _builder.Length = 0;
            _builder.Append(_recognition.BackendName)
                .Append("  ·  ").Append(Mathf.RoundToInt(_recognition.LastInferenceMs)).Append(" ms");

            if (_camera.IsRunning)
            {
                _builder.Append("  ·  ").Append(_camera.Width).Append('x').Append(_camera.Height)
                    .Append("  ·  rot ").Append(_camera.RotationAngle);
            }

            if (session != null)
            {
                _builder.Append("  ·  score ").Append(session.TotalScore);
            }

            _status.text = _builder.ToString();
        }

        private void ShowOnlyMessage(string text, int fontSize)
        {
            _title.text = string.Empty;
            _timer.text = string.Empty;
            _sequence.text = string.Empty;
            _sequenceSignature = -1;
            SetMessage(text, fontSize, Color.white);
        }

        private void SetMessage(string text, int fontSize, Color color)
        {
            _message.text = text;
            _message.fontSize = fontSize;
            _message.color = color;
        }
    }
}
