using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HandSeals.Core;
using Unity.InferenceEngine;
using UnityEngine;

namespace HandSeals.Recognition
{
    /// <summary>
    /// Runs the YOLOX-nano hand-seal model with Unity Sentis (package com.unity.ai.inference).
    /// Preprocessing and decoding live in HandSeals.Core; this class only moves data in and out of the model.
    /// </summary>
    public sealed class SentisYoloxDetector : IDisposable
    {
        public const int InputSize = 416;

        /// <summary>Path of the model under a Resources folder, without the extension.</summary>
        public const string DefaultModelResourcePath = "Models/yolox_nano";

        private readonly Worker _worker;
        private readonly TensorShape _inputShape = new TensorShape(1, 3, InputSize, InputSize);
        private readonly LetterboxPreprocessor _preprocessor = new LetterboxPreprocessor(InputSize);
        private readonly YoloxDecoder _decoder = new YoloxDecoder(InputSize, InputSize, YoloxLabels.ClassCount);
        private readonly List<Detection> _results = new List<Detection>();
        private bool _disposed;

        public SentisYoloxDetector(ModelAsset modelAsset, BackendType backend)
        {
            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            Backend = backend;
            _worker = new Worker(ModelLoader.Load(modelAsset), backend);
        }

        public BackendType Backend { get; }

        /// <summary>True while an inference is in flight; only one can run at a time.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>Detections with a score at or below this value are dropped.</summary>
        public float ScoreThreshold { get; set; } = 0.3f;

        /// <summary>Boxes overlapping a better one by more than this (IoU) are dropped.</summary>
        public float NmsThreshold { get; set; } = 0.45f;

        /// <summary>Geometry of the most recently processed frame.</summary>
        public LetterboxInfo LastFrame { get; private set; }

        /// <summary>Loads the model from Resources and picks the GPU when compute shaders are available.</summary>
        public static SentisYoloxDetector CreateFromResources(bool preferGpu = true, string resourcePath = DefaultModelResourcePath)
        {
            var modelAsset = Resources.Load<ModelAsset>(resourcePath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException(
                    $"Model 'Resources/{resourcePath}' was not found. Check that the .onnx file was imported by Sentis.");
            }

            BackendType backend = preferGpu && SystemInfo.supportsComputeShaders
                ? BackendType.GPUCompute
                : BackendType.CPU;

            return new SentisYoloxDetector(modelAsset, backend);
        }

        /// <summary>
        /// Detects seals in a camera frame. The returned list is reused by the next call.
        /// </summary>
        /// <param name="pixels">Frame pixels as returned by WebCamTexture.GetPixels32.</param>
        /// <param name="width">Width of the stored frame.</param>
        /// <param name="height">Height of the stored frame.</param>
        /// <param name="rotationDegrees">Clockwise rotation that makes the frame upright (WebCamTexture.videoRotationAngle).</param>
        /// <param name="bottomUp">True when the first row is the bottom of the picture (the Unity default).</param>
        public async Awaitable<IReadOnlyList<Detection>> DetectAsync(
            Color32[] pixels,
            int width,
            int height,
            int rotationDegrees,
            bool bottomUp)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SentisYoloxDetector));
            }

            if (IsBusy)
            {
                throw new InvalidOperationException("The previous detection has not finished yet.");
            }

            IsBusy = true;
            try
            {
                LastFrame = Preprocess(pixels, width, height, rotationDegrees, bottomUp);

                using (var input = new Tensor<float>(_inputShape, _preprocessor.Buffer))
                {
                    _worker.Schedule(input);

                    if (!(_worker.PeekOutput() is Tensor<float> output))
                    {
                        throw new InvalidOperationException("The model did not return a float tensor.");
                    }

                    // Waits for the GPU without blocking the main thread.
                    using (Tensor<float> onCpu = await output.ReadbackAndCloneAsync())
                    {
                        _results.Clear();
                        if (!_disposed)
                        {
                            Decode(onCpu);
                        }
                    }
                }

                return _results;
            }
            finally
            {
                IsBusy = false;
                if (_disposed)
                {
                    _worker.Dispose();
                }
            }
        }

        /// <summary>
        /// Copies the image the model has just seen into <paramref name="target"/> (InputSize x InputSize,
        /// rows bottom-up as Texture2D.SetPixels32 expects). For debugging orientation and colours.
        /// </summary>
        public void CopyModelInputTo(Color32[] target)
        {
            int plane = InputSize * InputSize;
            if (target == null || target.Length != plane)
            {
                throw new ArgumentException($"The target must hold {plane} pixels.", nameof(target));
            }

            float[] buffer = _preprocessor.Buffer;
            for (int y = 0; y < InputSize; y++)
            {
                int sourceRow = y * InputSize;
                int targetRow = (InputSize - 1 - y) * InputSize;
                for (int x = 0; x < InputSize; x++)
                {
                    int source = sourceRow + x;
                    target[targetRow + x] = new Color32(
                        (byte)buffer[2 * plane + source],
                        (byte)buffer[plane + source],
                        (byte)buffer[source],
                        255);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // While an inference is in flight the worker is released when it completes.
            if (!IsBusy)
            {
                _worker.Dispose();
            }
        }

        // Span-based work is kept out of the async method: C# 9 does not allow spans there.
        private LetterboxInfo Preprocess(Color32[] pixels, int width, int height, int rotationDegrees, bool bottomUp)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

            ReadOnlySpan<byte> rgba = MemoryMarshal.AsBytes(new ReadOnlySpan<Color32>(pixels));
            return _preprocessor.Fill(rgba, width, height, rotationDegrees, bottomUp);
        }

        private void Decode(Tensor<float> output)
        {
            _decoder.Decode(output.AsReadOnlySpan(), LastFrame, ScoreThreshold, NmsThreshold, _results);
        }
    }
}
