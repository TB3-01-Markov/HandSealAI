using HandSeals.Core;
using UnityEngine;

namespace HandSeals.Game
{
    /// <summary>
    /// Entry point of the app. Put this component on one object in the scene: it loads the saved progress
    /// and settings, creates the camera feed, recognition, sound, game flow and HUD, and wires them together.
    /// Player-facing options live in the in-game settings screen; the fields here are developer defaults.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private int cameraWidth = 640;
        [SerializeField] private int cameraHeight = 480;
        [SerializeField] private int cameraFps = 30;

        [Header("Recognition")]
        [Tooltip("Detections below this score are discarded by the decoder.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float scoreThreshold = 0.3f;

        [Header("Game")]
        [Range(0f, 5f)]
        [SerializeField] private float countdownSeconds = 3f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var repository = new JsonProgressRepository();
            PlayerProgress progress = repository.Load();
            GameSettings settings = progress.Settings;

            var cameraFeed = gameObject.AddComponent<CameraFeed>();
            cameraFeed.RequestedWidth = cameraWidth;
            cameraFeed.RequestedHeight = cameraHeight;
            cameraFeed.RequestedFps = cameraFps;

            var audioFeedback = gameObject.AddComponent<FeedbackAudio>();
            audioFeedback.SoundOn = settings.SoundOn;

            var recognition = gameObject.AddComponent<RecognitionController>();
            recognition.Initialize(cameraFeed, scoreThreshold, settings);

            var game = gameObject.AddComponent<GameController>();
            game.Initialize(
                cameraFeed,
                recognition,
                () => JsonJutsuCatalog.LoadFromResources(),
                repository,
                progress,
                countdownSeconds);

            game.SettingsChanged += changed =>
            {
                recognition.ApplySettings(changed);
                audioFeedback.SoundOn = changed.SoundOn;
            };

            var hud = gameObject.AddComponent<HudView>();
            hud.Initialize(cameraFeed, recognition, game, audioFeedback);
        }
    }
}
