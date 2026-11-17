using System;
using System.Collections.Generic;
using HandSeals.Core;
using HandSeals.Recognition;
using UnityEngine;

namespace HandSeals.Game
{
    /// <summary>
    /// Feeds camera frames to the detector and the best detection of each frame to the stabilizer.
    /// A new frame is sent only when the previous inference has finished, so the preview never waits for the model.
    /// If the GPU backend fails on a device, recognition switches to the CPU by itself.
    /// </summary>
    public sealed class RecognitionController : MonoBehaviour
    {
        private CameraFeed _camera;
        private SentisYoloxDetector _detector;
        private Color32[] _modelInputPixels;
        private float _scoreThreshold = 0.3f;
        private bool _paused;
        private bool _preferGpu = true;
        private bool _gpuFailed;
        private bool _rebuildPending;

        // Bumped whenever tracking restarts, so that a result from a frame taken before
        // (for example on the previous screen) is thrown away instead of counted.
        private int _generation;

        public SealStabilizer Stabilizer { get; } = new SealStabilizer();

        public RecognitionHintAdvisor Hints { get; } = new RecognitionHintAdvisor();

        /// <summary>Best detection of the latest processed frame, if any.</summary>
        public Detection? Best { get; private set; }

        /// <summary>Geometry of the latest processed frame (upright image size and scale).</summary>
        public LetterboxInfo Frame { get; private set; }

        /// <summary>Time from grabbing the frame to having detections, in milliseconds.</summary>
        public float LastInferenceMs { get; private set; }

        public string BackendName { get; private set; } = "-";

        /// <summary>Set when the model could not be loaded or run; recognition is then switched off.</summary>
        public string Error { get; private set; }

        /// <summary>When set, <see cref="ModelInputTexture"/> shows the image exactly as the model receives it.</summary>
        public bool CaptureModelInput { get; private set; }

        public Texture2D ModelInputTexture { get; private set; }

        /// <summary>While paused no frames go to the model (used in the menu to save battery).</summary>
        public bool Paused
        {
            get { return _paused; }
            set
            {
                if (_paused == value)
                {
                    return;
                }

                _paused = value;
                ResetTracking();
            }
        }

        public event Action<SealType> SealConfirmed
        {
            add { Stabilizer.SealConfirmed += value; }
            remove { Stabilizer.SealConfirmed -= value; }
        }

        public void Initialize(CameraFeed cameraFeed, float scoreThreshold, GameSettings settings)
        {
            _camera = cameraFeed;
            _scoreThreshold = scoreThreshold;
            _preferGpu = settings.UseGpu;
            ApplySettings(settings);
            Rebuild();
        }

        /// <summary>Applies the player's settings; a backend change takes effect after the running inference.</summary>
        public void ApplySettings(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Stabilizer.MinScore = settings.ConfirmScore;
            Stabilizer.HoldSeconds = settings.HoldSeconds;
            CaptureModelInput = settings.ShowModelInput;

            if (settings.UseGpu != _preferGpu)
            {
                _preferGpu = settings.UseGpu;
                _gpuFailed = false;
                _rebuildPending = true;
            }
        }

        /// <summary>Forgets the current detection, hold and hint timers; call when a new task starts.</summary>
        public void ResetTracking()
        {
            _generation++;
            Best = null;
            Stabilizer.Reset();
            Hints.Reset(Time.unscaledTimeAsDouble);
        }

        private void Update()
        {
            if (_rebuildPending && (_detector == null || !_detector.IsBusy))
            {
                Rebuild();
            }

            if (_paused || _detector == null || _camera == null || _detector.IsBusy || !_camera.DidUpdateThisFrame)
            {
                return;
            }

            RunDetection();
        }

        private void Rebuild()
        {
            _rebuildPending = false;
            DisposeDetector();
            Error = null;

            bool useGpu = _preferGpu && !_gpuFailed;
            try
            {
                _detector = SentisYoloxDetector.CreateFromResources(useGpu);
                _detector.ScoreThreshold = _scoreThreshold;
                BackendName = _detector.Backend + (_gpuFailed ? " (GPU failed)" : string.Empty);
            }
            catch (Exception exception)
            {
                HandleFailure(exception, useGpu);
            }

            ResetTracking();
        }

        private async void RunDetection()
        {
            SentisYoloxDetector detector = _detector;
            int generation = _generation;

            try
            {
                double startedAt = Time.realtimeSinceStartupAsDouble;

                Color32[] pixels = _camera.ReadPixels();
                IReadOnlyList<Detection> detections = await detector.DetectAsync(
                    pixels,
                    _camera.Width,
                    _camera.Height,
                    _camera.RotationAngle,
                    !_camera.VerticallyMirrored);

                if (this == null || detector != _detector || generation != _generation || _paused)
                {
                    // Destroyed, rebuilt, paused or restarted while the model was running: the result is stale.
                    return;
                }

                LastInferenceMs = (float)((Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0);
                Frame = detector.LastFrame;

                // The decoder returns detections sorted by score, best first.
                Best = detections.Count > 0 ? detections[0] : (Detection?)null;

                double now = Time.unscaledTimeAsDouble;
                Stabilizer.Update(
                    Best.HasValue ? Best.Value.Seal : SealType.None,
                    Best.HasValue ? Best.Value.Score : 0f,
                    now);

                bool confident = Best.HasValue && Best.Value.Seal.IsSeal() && Best.Value.Score >= Stabilizer.MinScore;
                Hints.Update(Best.HasValue, confident, now);

                if (CaptureModelInput)
                {
                    UpdateModelInputTexture();
                }
            }
            catch (Exception exception)
            {
                if (this != null && detector == _detector)
                {
                    HandleFailure(exception, detector.UsesGpu);
                }
            }
        }

        private void UpdateModelInputTexture()
        {
            const int size = SentisYoloxDetector.InputSize;

            if (ModelInputTexture == null)
            {
                ModelInputTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                _modelInputPixels = new Color32[size * size];
            }

            _detector.CopyModelInputTo(_modelInputPixels);
            ModelInputTexture.SetPixels32(_modelInputPixels);
            ModelInputTexture.Apply(false);
        }

        private void HandleFailure(Exception exception, bool wasGpu)
        {
            Debug.LogException(exception, this);
            DisposeDetector();

            if (wasGpu && !_gpuFailed)
            {
                // Some phones have compute-shader drivers Sentis cannot use: try once more on the CPU.
                Debug.LogWarning("HandSealAI: the GPU backend failed, switching to the CPU.");
                _gpuFailed = true;
                _rebuildPending = true;
                return;
            }

            Error = exception.Message;
            Best = null;
            Stabilizer.Reset();
        }

        private void OnDestroy()
        {
            DisposeDetector();

            if (ModelInputTexture != null)
            {
                Destroy(ModelInputTexture);
            }
        }

        private void DisposeDetector()
        {
            if (_detector != null)
            {
                _detector.Dispose();
                _detector = null;
            }
        }
    }
}
