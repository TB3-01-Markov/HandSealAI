using System;
using System.Collections.Generic;

namespace HandSeals.Core
{
    /// <summary>
    /// Decodes the raw output of a YOLOX model (one row per anchor: x, y, w, h, objectness, class scores)
    /// into detections in image pixels and removes overlapping boxes (class-agnostic NMS).
    /// A C# port of the post-processing in the model author's Python demo.
    /// </summary>
    public sealed class YoloxDecoder
    {
        private static readonly int[] Strides = { 8, 16, 32 };

        private readonly int _classCount;
        private readonly int _rowLength;
        private readonly int[] _gridX;
        private readonly int[] _gridY;
        private readonly int[] _stride;
        private readonly List<Detection> _candidates = new List<Detection>();

        public YoloxDecoder(int inputWidth = 416, int inputHeight = 416, int classCount = YoloxLabels.ClassCount)
        {
            if (inputWidth <= 0 || inputWidth % 32 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputWidth), "Input width must be a positive multiple of 32.");
            }

            if (inputHeight <= 0 || inputHeight % 32 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputHeight), "Input height must be a positive multiple of 32.");
            }

            if (classCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(classCount));
            }

            _classCount = classCount;
            _rowLength = 5 + classCount;

            int anchors = 0;
            foreach (int stride in Strides)
            {
                anchors += (inputWidth / stride) * (inputHeight / stride);
            }

            _gridX = new int[anchors];
            _gridY = new int[anchors];
            _stride = new int[anchors];

            int index = 0;
            foreach (int stride in Strides)
            {
                int columns = inputWidth / stride;
                int rows = inputHeight / stride;
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        _gridX[index] = x;
                        _gridY[index] = y;
                        _stride[index] = stride;
                        index++;
                    }
                }
            }
        }

        /// <summary>Number of anchors (rows) the model outputs: 3549 for a 416 x 416 input.</summary>
        public int AnchorCount => _gridX.Length;

        /// <summary>Expected length of the flattened output tensor.</summary>
        public int ExpectedLength => AnchorCount * _rowLength;

        /// <summary>
        /// Decodes the output tensor and writes the surviving detections, best score first, into <paramref name="results"/>.
        /// </summary>
        public void Decode(
            ReadOnlySpan<float> output,
            LetterboxInfo frame,
            float scoreThreshold,
            float nmsThreshold,
            List<Detection> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (output.Length != ExpectedLength)
            {
                throw new ArgumentException(
                    $"Expected {ExpectedLength} values ({AnchorCount} x {_rowLength}) but got {output.Length}.",
                    nameof(output));
            }

            if (frame.Ratio <= 0f)
            {
                throw new ArgumentException("The letterbox ratio must be positive.", nameof(frame));
            }

            results.Clear();
            _candidates.Clear();

            float inverseRatio = 1f / frame.Ratio;

            for (int anchor = 0; anchor < _gridX.Length; anchor++)
            {
                int offset = anchor * _rowLength;
                float objectness = output[offset + 4];
                if (objectness <= scoreThreshold)
                {
                    // Class scores never exceed 1, so the product cannot pass the threshold either.
                    continue;
                }

                int bestClass = 0;
                float bestClassScore = output[offset + 5];
                for (int c = 1; c < _classCount; c++)
                {
                    float classScore = output[offset + 5 + c];
                    if (classScore > bestClassScore)
                    {
                        bestClassScore = classScore;
                        bestClass = c;
                    }
                }

                float score = objectness * bestClassScore;
                if (score <= scoreThreshold)
                {
                    continue;
                }

                int stride = _stride[anchor];
                float centerX = (output[offset] + _gridX[anchor]) * stride;
                float centerY = (output[offset + 1] + _gridY[anchor]) * stride;
                float width = (float)Math.Exp(output[offset + 2]) * stride;
                float height = (float)Math.Exp(output[offset + 3]) * stride;

                var box = new BoundingBox(
                    (centerX - width * 0.5f) * inverseRatio,
                    (centerY - height * 0.5f) * inverseRatio,
                    (centerX + width * 0.5f) * inverseRatio,
                    (centerY + height * 0.5f) * inverseRatio);

                _candidates.Add(new Detection(YoloxLabels.ToSeal(bestClass), score, box));
            }

            _candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            for (int i = 0; i < _candidates.Count; i++)
            {
                Detection candidate = _candidates[i];
                bool suppressed = false;

                for (int k = 0; k < results.Count; k++)
                {
                    if (Overlap(results[k].Box, candidate.Box) > nmsThreshold)
                    {
                        suppressed = true;
                        break;
                    }
                }

                if (!suppressed)
                {
                    results.Add(candidate);
                }
            }

            for (int i = 0; i < results.Count; i++)
            {
                Detection kept = results[i];
                results[i] = new Detection(kept.Seal, kept.Score, kept.Box.ClampTo(frame.ImageWidth, frame.ImageHeight));
            }
        }

        // Intersection over union with the inclusive-pixel (+1) convention of the reference implementation.
        private static float Overlap(BoundingBox a, BoundingBox b)
        {
            float areaA = (a.XMax - a.XMin + 1f) * (a.YMax - a.YMin + 1f);
            float areaB = (b.XMax - b.XMin + 1f) * (b.YMax - b.YMin + 1f);

            float width = Math.Max(0f, Math.Min(a.XMax, b.XMax) - Math.Max(a.XMin, b.XMin) + 1f);
            float height = Math.Max(0f, Math.Min(a.YMax, b.YMax) - Math.Max(a.YMin, b.YMin) + 1f);
            float intersection = width * height;

            return intersection / (areaA + areaB - intersection);
        }
    }
}
