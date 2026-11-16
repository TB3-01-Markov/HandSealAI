using UnityEngine;

namespace HandSeals.Game
{
    /// <summary>
    /// Entry point of the app. Put this component on one object in the scene:
    /// it creates the camera feed, the recognition, the game loop and the HUD and wires them together.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private int cameraWidth = 640;
        [SerializeField] private int cameraHeight = 480;
        [SerializeField] private int cameraFps = 30;

        [Header("Recognition")]
        [Tooltip("Run the model on the GPU when the device supports compute shaders.")]
        [SerializeField] private bool preferGpu = true;

        [Tooltip("Detections below this score are discarded by the decoder.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float scoreThreshold = 0.3f;

        [Tooltip("A seal must reach this score to start the hold timer.")]
        [Range(0.1f, 0.95f)]
        [SerializeField] private float confirmScore = 0.6f;

        [Tooltip("How long a seal must be held before it counts, in seconds.")]
        [Range(0f, 2f)]
        [SerializeField] private float holdSeconds = 0.4f;

        [Header("Game")]
        [Range(0f, 5f)]
        [SerializeField] private float countdownSeconds = 3f;

        [Header("Debug")]
        [Tooltip("Show the 416 x 416 image exactly as the model receives it. Useful for checking camera orientation.")]
        [SerializeField] private bool showModelInput;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var cameraFeed = gameObject.AddComponent<CameraFeed>();
            cameraFeed.RequestedWidth = cameraWidth;
            cameraFeed.RequestedHeight = cameraHeight;
            cameraFeed.RequestedFps = cameraFps;

            var recognition = gameObject.AddComponent<RecognitionController>();
            recognition.Initialize(cameraFeed, preferGpu, scoreThreshold, confirmScore, holdSeconds);

            var game = gameObject.AddComponent<GameController>();
            game.Initialize(
                cameraFeed,
                recognition,
                () => JsonJutsuCatalog.LoadFromResources(),
                new JsonProgressRepository(),
                countdownSeconds);

            var hud = gameObject.AddComponent<HudView>();
            hud.Initialize(cameraFeed, recognition, game, showModelInput);
        }
    }
}
