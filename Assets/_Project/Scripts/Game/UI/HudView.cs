using System.Text;
using HandSeals.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HandSeals.Game
{
    /// <summary>
    /// Everything on screen, built from code so that no prefabs are needed: the mirrored camera preview with
    /// the detection box, the main menu, and the play screen shared by the training and the technique mode
    /// (task at the top, recognised seal with a hold bar at the bottom).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float FeedbackSeconds = 0.5f;
        private const float TrainingFeedbackSeconds = 0.9f;
        private const float StatusInterval = 0.25f;
        private const float BoxThickness = 6f;
        private const int NoKey = int.MinValue;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelGoodColor = new Color(0.06f, 0.4f, 0.2f, 0.8f);
        private static readonly Color PanelBadColor = new Color(0.55f, 0.08f, 0.08f, 0.8f);
        private static readonly Color GoodColor = new Color(0.43f, 0.91f, 0.63f);
        private static readonly Color BadColor = new Color(0.96f, 0.42f, 0.42f);
        private static readonly Color MutedColor = new Color(0.62f, 0.62f, 0.62f);

        private CameraFeed _camera;
        private RecognitionController _recognition;
        private GameController _game;

        // Preview
        private RectTransform _previewArea;
        private RectTransform _mirrorRoot;
        private RawImage _preview;
        private RectTransform _box;
        private Image[] _boxEdges;
        private float _previewScale = 1f;

        // Screens
        private RectTransform _safeRoot;
        private GameObject _dim;
        private GameObject _playRoot;
        private GameObject _skipButton;
        private GameObject _allowCameraButton;
        private MenuPanel _menu;

        // Play screen
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

        // What is currently shown, so that strings are rebuilt only when something changed.
        private readonly StringBuilder _builder = new StringBuilder(256);
        private GameMode? _shownMode;
        private Jutsu _shownJutsu;
        private int _shownState = NoKey;
        private int _shownProgress = NoKey;
        private int _shownTarget = NoKey;
        private int _shownCorrect = NoKey;
        private int _timerKey = NoKey;
        private int _messageKey = NoKey;
        private int _sealKey = NoKey;
        private float _nextStatusTime;
        private float _safeX = -1f;
        private float _safeY = -1f;
        private float _safeWidth = -1f;
        private float _safeHeight = -1f;
        private int _screenWidth;
        private int _screenHeight;

        public void Initialize(CameraFeed cameraFeed, RecognitionController recognition, GameController game, bool showModelInput)
        {
            _camera = cameraFeed;
            _recognition = recognition;
            _game = game;
            _recognition.CaptureModelInput = showModelInput;

            UiInput.EnsureEventSystem();
            Build(showModelInput);

            _menu.TrainingClicked += _game.StartTraining;
            _menu.PlayAllClicked += () => _game.StartChallenge(0, true);
            _menu.TechniqueClicked += index => _game.StartChallenge(index, false);
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            ApplySafeArea();

            if (UiInput.BackPressed())
            {
                if (_game.Mode == GameMode.Menu)
                {
                    Application.Quit();
                }
                else
                {
                    _game.OpenMenu();
                }
            }

            SyncMode();
            UpdatePreview();

            if (_game.Mode == GameMode.Menu)
            {
                return;
            }

            UpdateRecognition();

            if (!ShowProblem())
            {
                if (_game.Mode == GameMode.Training)
                {
                    UpdateTraining();
                }
                else
                {
                    UpdateChallenge();
                }
            }

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

            // Without a raycaster the buttons would never receive taps.
            canvasObject.AddComponent<GraphicRaycaster>();

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

            // Darkens the preview behind the menu.
            RectTransform dim = UiFactory.CreateRect("Dim", root, Vector2.zero, Vector2.one);
            UiFactory.AddImage(dim, new Color(0f, 0f, 0f, 0.72f));
            _dim = dim.gameObject;

            // Everything with text or buttons stays inside the safe area (clear of notches and rounded corners).
            _safeRoot = UiFactory.CreateRect("SafeArea", root, Vector2.zero, Vector2.one);

            BuildPlayScreen(_safeRoot);

            // Centre message: countdown, results, camera problems.
            _message = UiFactory.AddText(
                UiFactory.CreateRect("Message", _safeRoot, new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.66f)),
                88, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.AddOutline(_message);

            RectTransform allow = UiFactory.CreateRect("AllowCameraButton", _safeRoot, new Vector2(0.2f, 0.28f), new Vector2(0.8f, 0.35f));
            UiFactory.AddButton(allow, "<b>Allow camera</b>", 52, TextAnchor.MiddleCenter, UiFactory.AccentButtonColor, _camera.RequestPermission);
            _allowCameraButton = allow.gameObject;
            _allowCameraButton.SetActive(false);

            _menu = new MenuPanel(_safeRoot);

            if (showModelInput)
            {
                // Debug view in the top-right corner, just below the top panel.
                var modelInputObject = new GameObject("ModelInput", typeof(RectTransform));
                var modelInputRect = (RectTransform)modelInputObject.transform;
                modelInputRect.SetParent(_safeRoot, false);
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

        private void BuildPlayScreen(Transform parent)
        {
            RectTransform play = UiFactory.CreateRect("Play", parent, Vector2.zero, Vector2.one);
            _playRoot = play.gameObject;

            // Top panel: the task (technique and its sequence, or the seal to practise).
            RectTransform top = UiFactory.CreateRect("TopPanel", play, new Vector2(0f, 0.82f), Vector2.one);
            _topPanel = UiFactory.AddImage(top, PanelColor);
            _title = UiFactory.AddText(
                UiFactory.CreateRect("Title", top, new Vector2(0.04f, 0.5f), new Vector2(0.7f, 0.95f)),
                60, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            _timer = UiFactory.AddText(
                UiFactory.CreateRect("Timer", top, new Vector2(0.7f, 0.5f), new Vector2(0.96f, 0.95f)),
                64, TextAnchor.MiddleRight, Color.white, FontStyle.Bold);
            _sequence = UiFactory.AddText(
                UiFactory.CreateRect("Sequence", top, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.5f)),
                46, TextAnchor.MiddleLeft, Color.white);

            // Bottom panel: what the model sees right now.
            RectTransform bottom = UiFactory.CreateRect("BottomPanel", play, Vector2.zero, new Vector2(1f, 0.17f));
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

            // Buttons sit just above the bottom panel, where they do not cover the hands.
            UiFactory.AddButton(
                UiFactory.CreateRect("BackButton", play, new Vector2(0.04f, 0.183f), new Vector2(0.3f, 0.235f)),
                "‹ Menu", 42, TextAnchor.MiddleCenter, PanelColor, _game.OpenMenu);

            RectTransform skip = UiFactory.CreateRect("SkipButton", play, new Vector2(0.7f, 0.183f), new Vector2(0.96f, 0.235f));
            UiFactory.AddButton(skip, "Skip ›", 42, TextAnchor.MiddleCenter, PanelColor, _game.SkipTrainingSeal);
            _skipButton = skip.gameObject;
        }

        private Image CreateEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform edge = UiFactory.CreateRect(edgeName, _box, anchorMin, anchorMax);
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            return UiFactory.AddImage(edge, Color.white);
        }

        // ---------------------------------------------------------------- screens

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            int width = Screen.width;
            int height = Screen.height;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (safe.x == _safeX && safe.y == _safeY && safe.width == _safeWidth && safe.height == _safeHeight
                && width == _screenWidth && height == _screenHeight)
            {
                return;
            }

            _safeX = safe.x;
            _safeY = safe.y;
            _safeWidth = safe.width;
            _safeHeight = safe.height;
            _screenWidth = width;
            _screenHeight = height;

            _safeRoot.anchorMin = new Vector2(safe.x / width, safe.y / height);
            _safeRoot.anchorMax = new Vector2((safe.x + safe.width) / width, (safe.y + safe.height) / height);
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private void SyncMode()
        {
            GameMode mode = _game.Mode;
            if (_shownMode.HasValue && _shownMode.Value == mode)
            {
                return;
            }

            _shownMode = mode;
            bool inMenu = mode == GameMode.Menu;

            _dim.SetActive(inMenu);
            _menu.SetVisible(inMenu);
            _playRoot.SetActive(!inMenu);
            _skipButton.SetActive(mode == GameMode.Training);
            _allowCameraButton.SetActive(false);
            _box.gameObject.SetActive(false);

            if (inMenu)
            {
                _menu.Refresh(_game);
            }

            ResetPlayTexts();
        }

        private void ResetPlayTexts()
        {
            _shownJutsu = null;
            _shownState = NoKey;
            _shownProgress = NoKey;
            _shownTarget = NoKey;
            _shownCorrect = NoKey;
            _timerKey = NoKey;
            _messageKey = NoKey;
            _sealKey = NoKey;
            _nextStatusTime = 0f;

            _title.text = string.Empty;
            _timer.text = string.Empty;
            _timer.color = Color.white;
            _sequence.text = string.Empty;
            _message.text = string.Empty;
            _topPanel.color = PanelColor;
        }

        /// <summary>Shows what stops the game (model, data or camera trouble). Returns false when all is well.</summary>
        private bool ShowProblem()
        {
            string problem = null;
            int key = 0;
            bool canAllow = false;

            if (_recognition.Error != null)
            {
                problem = "Model error:\n" + _recognition.Error;
                key = 901;
            }
            else if (_game.Error != null)
            {
                problem = "Game data error:\n" + _game.Error;
                key = 902;
            }
            else if (_camera.State == CameraFeedState.Starting)
            {
                problem = "Starting the camera…";
                key = 903;
            }
            else if (_camera.State == CameraFeedState.PermissionDenied)
            {
                problem = "The game needs the camera\nto see your hands.\n<size=36>If the button does nothing, allow the camera\nfor this app in the system settings.</size>";
                key = 904;
                canAllow = true;
            }
            else if (_camera.State == CameraFeedState.NoCamera)
            {
                problem = "No camera found.";
                key = 905;
            }

            if (_allowCameraButton.activeSelf != canAllow)
            {
                _allowCameraButton.SetActive(canAllow);
            }

            if (problem == null)
            {
                return false;
            }

            if (_messageKey != key)
            {
                ResetPlayTexts();
                _messageKey = key;
                _message.text = problem;
                _message.fontSize = 52;
                _message.color = Color.white;
            }

            return true;
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
            if (_box.gameObject.activeSelf != hasBox)
            {
                _box.gameObject.SetActive(hasBox);
            }

            bool confirmed = stabilizer.IsConfirmed;
            Color accent = confirmed ? GoodColor : Color.white;

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

            int sealKey = -1;
            if (best.HasValue)
            {
                int percent = Mathf.RoundToInt(best.Value.Score * 100f);
                sealKey = ((int)best.Value.Seal << 8) | percent | (confirmed ? 1 << 16 : 0);
            }

            if (sealKey != _sealKey)
            {
                _sealKey = sealKey;
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
            }

            float hold = (float)stabilizer.GetHoldProgress(Time.unscaledTimeAsDouble);
            _holdFill.anchorMax = new Vector2(hold, 1f);
            _holdFillImage.color = accent;
        }

        private void UpdateChallenge()
        {
            GameSession session = _game.Session;
            Jutsu jutsu = session != null ? session.Current : null;
            if (jutsu == null)
            {
                return;
            }

            SessionState state = session.State;

            if (!ReferenceEquals(jutsu, _shownJutsu) || (int)state != _shownState || session.Progress != _shownProgress)
            {
                _shownJutsu = jutsu;
                _shownState = (int)state;
                _shownProgress = session.Progress;
                _timerKey = NoKey;

                _title.text = _game.TryGetBestTime(jutsu, out double bestSeconds)
                    ? jutsu.Name + "\n<size=34><color=#9E9E9E>best " + bestSeconds.ToString("0.0") + " s</color></size>"
                    : jutsu.Name;

                _sequence.text = BuildSequence(jutsu, session);
            }

            _topPanel.color = FeedbackColor(FeedbackSeconds);

            switch (state)
            {
                case SessionState.Countdown:
                    SetTimer(-1, string.Empty, Color.white);
                    int digit = Mathf.Max(1, Mathf.CeilToInt((float)session.CountdownLeft));
                    if (_messageKey != 100 + digit)
                    {
                        SetMessage(100 + digit, digit.ToString(), 220, Color.white);
                    }

                    break;

                case SessionState.WaitingForSeal:
                case SessionState.Paused:
                    int tenths = Mathf.CeilToInt((float)(session.TimeLeft * 10.0));
                    if (tenths != _timerKey)
                    {
                        SetTimer(tenths, (tenths / 10f).ToString("0.0") + " s", session.TimeLeft < 3.0 ? BadColor : Color.white);
                    }

                    if (state == SessionState.Paused)
                    {
                        if (_messageKey != 2)
                        {
                            SetMessage(2, "Paused", 88, Color.white);
                        }
                    }
                    else if (_messageKey != 1)
                    {
                        SetMessage(1, string.Empty, 88, Color.white);
                    }

                    break;

                case SessionState.Completed:
                    SetTimer(-1, string.Empty, Color.white);
                    if (_messageKey != 3)
                    {
                        SetMessage(
                            3,
                            jutsu.Name + "!\n<size=56>+" + session.LastScore + "  ·  " + _game.LastTime.ToString("0.0") + " s"
                            + (_game.LastWasRecord ? "  ·  new best" : string.Empty)
                            + (session.Combo > 1 ? "\ncombo x" + session.Combo : string.Empty) + "</size>",
                            88,
                            GoodColor);
                    }

                    break;

                case SessionState.Failed:
                    SetTimer(-2, "0.0 s", BadColor);
                    if (_messageKey != 4)
                    {
                        SetMessage(4, "Time's up\n<size=56>try again</size>", 88, BadColor);
                    }

                    break;

                default:
                    SetTimer(-1, string.Empty, Color.white);
                    if (_messageKey != 1)
                    {
                        SetMessage(1, string.Empty, 88, Color.white);
                    }

                    break;
            }
        }

        private void UpdateTraining()
        {
            TrainingSession training = _game.Training;
            if (training == null)
            {
                return;
            }

            if ((int)training.Target != _shownTarget || training.CorrectCount != _shownCorrect)
            {
                _shownTarget = (int)training.Target;
                _shownCorrect = training.CorrectCount;

                _title.text = "Training";
                _timer.text = training.CorrectCount + " done";
                _timer.color = Color.white;
                _sequence.text = "Make this seal:   <size=84><b>" + training.Target + "</b></size>";
            }

            _topPanel.color = FeedbackColor(TrainingFeedbackSeconds);

            // Tell the player what the model saw: a named mistake teaches more than a red flash.
            bool recent = Time.unscaledTimeAsDouble - _game.LastMatchTime < TrainingFeedbackSeconds;
            if (recent && _game.LastMatch == MatchResult.Correct)
            {
                if (_messageKey != 201)
                {
                    SetMessage(201, "Correct!", 110, GoodColor);
                }
            }
            else if (recent && _game.LastMatch == MatchResult.Wrong)
            {
                int key = 300 + (int)_game.LastSeal;
                if (_messageKey != key)
                {
                    SetMessage(key, "That was " + _game.LastSeal, 88, BadColor);
                }
            }
            else if (_messageKey != 1)
            {
                SetMessage(1, string.Empty, 88, Color.white);
            }
        }

        private void UpdateStatus()
        {
            if (Time.unscaledTime < _nextStatusTime)
            {
                return;
            }

            _nextStatusTime = Time.unscaledTime + StatusInterval;

            _builder.Length = 0;
            _builder.Append(_recognition.BackendName)
                .Append("  ·  ").Append(Mathf.RoundToInt(_recognition.LastInferenceMs)).Append(" ms");

            if (_camera.IsRunning)
            {
                _builder.Append("  ·  ").Append(_camera.Width).Append('x').Append(_camera.Height)
                    .Append("  ·  rot ").Append(_camera.RotationAngle);
            }

            TrainingSession training = _game.Training;
            if (_game.Mode == GameMode.Training && training != null)
            {
                _builder.Append("  ·  mistakes ").Append(training.Mistakes);
                if (training.CorrectCount > 0)
                {
                    _builder.Append("  ·  fastest ").Append(training.BestSeconds.ToString("0.0")).Append(" s");
                }
            }
            else if (_game.Session != null)
            {
                _builder.Append("  ·  score ").Append(_game.Session.TotalScore);
            }

            _status.text = _builder.ToString();
        }

        // ---------------------------------------------------------------- helpers

        private string BuildSequence(Jutsu jutsu, GameSession session)
        {
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

            return _builder.ToString();
        }

        /// <summary>Panel colour for a short time after a confirmed seal: green for right, red for wrong.</summary>
        private Color FeedbackColor(float seconds)
        {
            if (Time.unscaledTimeAsDouble - _game.LastMatchTime < seconds)
            {
                if (_game.LastMatch == MatchResult.Wrong)
                {
                    return PanelBadColor;
                }

                if (_game.LastMatch == MatchResult.Correct || _game.LastMatch == MatchResult.Completed)
                {
                    return PanelGoodColor;
                }
            }

            return PanelColor;
        }

        private void SetTimer(int key, string text, Color color)
        {
            if (_timerKey == key)
            {
                return;
            }

            _timerKey = key;
            _timer.text = text;
            _timer.color = color;
        }

        private void SetMessage(int key, string text, int fontSize, Color color)
        {
            _messageKey = key;
            _message.text = text;
            _message.fontSize = fontSize;
            _message.color = color;
        }
    }
}
