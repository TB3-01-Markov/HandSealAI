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
    /// </summary>
    public sealed class RecognitionController : MonoBehaviour
    {
        private CameraFeed _camera;
        private SentisYoloxDetector _detector;
        private Color32[] _modelInputPixels;

        public SealStabilizer Stabilizer { get; } = new SealStabilizer();

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
        public bool CaptureModelInput { get; set; }

        public Texture2D ModelInputTexture { get; private set; }

        public event Action<SealType> SealConfirmed
        {
            add { Stabilizer.SealConfirmed += value; }
            remove { Stabilizer.SealConfirmed -= value; }
        }

        public void Initialize(CameraFeed cameraFeed, bool preferGpu, float scoreThreshold, float minConfirmScore, double holdSeconds)
        {
            _camera = cameraFeed;
            Stabilizer.MinScore = minConfirmScore;
            Stabilizer.HoldSeconds = holdSeconds;

            try
            {
                _detector = SentisYoloxDetector.CreateFromResources(preferGpu);
                _detector.ScoreThreshold = scoreThreshold;
                BackendName = _detector.Backend.ToString();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Update()
        {
            if (_detector == null || _camera == null || _detector.IsBusy || !_camera.DidUpdateThisFrame)
            {
                return;
            }

            RunDetection();
        }

        private async void RunDetection()
        {
            try
            {
                double startedAt = Time.realtimeSinceStartupAsDouble;

                Color32[] pixels = _camera.ReadPixels();
                IReadOnlyList<Detection> detections = await _detector.DetectAsync(
                    pixels,
                    _camera.Width,
                    _camera.Height,
                    _camera.RotationAngle,
                    !_camera.VerticallyMirrored);

                if (this == null || _detector == null)
                {
                    // Destroyed while the model was running.
                    return;
                }

                LastInferenceMs = (float)((Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0);
                Frame = _detector.LastFrame;

                // The decoder returns detections sorted by score, best first.
                Best = detections.Count > 0 ? detections[0] : (Detection?)null;

                Stabilizer.Update(
                    Best.HasValue ? Best.Value.Seal : SealType.None,
                    Best.HasValue ? Best.Value.Score : 0f,
                    Time.unscaledTimeAsDouble);

                if (CaptureModelInput)
                {
                    UpdateModelInputTexture();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
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

        private void Fail(Exception exception)
        {
            Debug.LogException(exception, this);
            Error = exception.Message;
            Best = null;
            Stabilizer.Reset();
            DisposeDetector();
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
