using System;

namespace HandSeals.Core
{
    /// <summary>
    /// Turns an RGBA camera frame into the input the YOLOX model was trained on:
    /// the upright image scaled to fit a square, placed in the top-left corner, padded with grey (114),
    /// planar BGR, values 0..255. Mirrors the preprocessing of the model author's Python demo.
    /// </summary>
    public sealed class LetterboxPreprocessor
    {
        public const float PadValue = 114f;

        private readonly int[] _x0;
        private readonly int[] _x1;
        private readonly float[] _wx;

        public LetterboxPreprocessor(int inputSize = 416)
        {
            if (inputSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputSize));
            }

            InputSize = inputSize;
            Buffer = new float[3 * inputSize * inputSize];
            _x0 = new int[inputSize];
            _x1 = new int[inputSize];
            _wx = new float[inputSize];
        }

        /// <summary>Side of the square model input in pixels.</summary>
        public int InputSize { get; }

        /// <summary>Model input: three planes (B, G, R) of InputSize x InputSize, rows from top to bottom.</summary>
        public float[] Buffer { get; }

        /// <summary>
        /// Fills <see cref="Buffer"/> from a camera frame.
        /// </summary>
        /// <param name="rgba">Pixels, 4 bytes each (R, G, B, A), row by row.</param>
        /// <param name="sourceWidth">Width of the frame as stored.</param>
        /// <param name="sourceHeight">Height of the frame as stored.</param>
        /// <param name="rotationDegrees">Clockwise rotation (0, 90, 180, 270) that makes the frame upright.</param>
        /// <param name="sourceIsBottomUp">True when the first row is the bottom of the picture (Unity textures).</param>
        public LetterboxInfo Fill(
            ReadOnlySpan<byte> rgba,
            int sourceWidth,
            int sourceHeight,
            int rotationDegrees,
            bool sourceIsBottomUp)
        {
            if (sourceWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            }

            if (sourceHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceHeight));
            }

            if (rgba.Length < sourceWidth * sourceHeight * 4)
            {
                throw new ArgumentException("The pixel buffer is smaller than width * height * 4.", nameof(rgba));
            }

            int rotation = ((rotationDegrees % 360) + 360) % 360;
            if (rotation % 90 != 0)
            {
                throw new ArgumentException("Rotation must be a multiple of 90 degrees.", nameof(rotationDegrees));
            }

            bool swapAxes = rotation == 90 || rotation == 270;
            int imageWidth = swapAxes ? sourceHeight : sourceWidth;
            int imageHeight = swapAxes ? sourceWidth : sourceHeight;

            float ratio = Math.Min(InputSize / (float)imageHeight, InputSize / (float)imageWidth);
            int scaledWidth = Math.Max(1, Math.Min(InputSize, (int)(imageWidth * ratio)));
            int scaledHeight = Math.Max(1, Math.Min(InputSize, (int)(imageHeight * ratio)));

            // The index of the source pixel is an affine function of the upright coordinates,
            // so three probes are enough to get its coefficients.
            int origin = SourcePixelIndex(0, 0, sourceWidth, sourceHeight, rotation, sourceIsBottomUp);
            int stepX = SourcePixelIndex(1, 0, sourceWidth, sourceHeight, rotation, sourceIsBottomUp) - origin;
            int stepY = SourcePixelIndex(0, 1, sourceWidth, sourceHeight, rotation, sourceIsBottomUp) - origin;

            float scaleX = imageWidth / (float)scaledWidth;
            float scaleY = imageHeight / (float)scaledHeight;

            for (int x = 0; x < scaledWidth; x++)
            {
                Sample(x, scaleX, imageWidth, out int first, out int second, out float weight);
                _x0[x] = first * stepX;
                _x1[x] = second * stepX;
                _wx[x] = weight;
            }

            float[] buffer = Buffer;
            int size = InputSize;
            int plane = size * size;
            Array.Fill(buffer, PadValue);

            for (int y = 0; y < scaledHeight; y++)
            {
                Sample(y, scaleY, imageHeight, out int firstRow, out int secondRow, out float wy);
                int row0 = origin + firstRow * stepY;
                int row1 = origin + secondRow * stepY;
                int target = y * size;

                for (int x = 0; x < scaledWidth; x++)
                {
                    int i00 = (row0 + _x0[x]) * 4;
                    int i01 = (row0 + _x1[x]) * 4;
                    int i10 = (row1 + _x0[x]) * 4;
                    int i11 = (row1 + _x1[x]) * 4;
                    float wx = _wx[x];

                    float r = Blend(rgba[i00], rgba[i01], rgba[i10], rgba[i11], wx, wy);
                    float g = Blend(rgba[i00 + 1], rgba[i01 + 1], rgba[i10 + 1], rgba[i11 + 1], wx, wy);
                    float b = Blend(rgba[i00 + 2], rgba[i01 + 2], rgba[i10 + 2], rgba[i11 + 2], wx, wy);

                    buffer[target + x] = b;
                    buffer[plane + target + x] = g;
                    buffer[2 * plane + target + x] = r;
                }
            }

            return new LetterboxInfo(imageWidth, imageHeight, scaledWidth, scaledHeight, ratio);
        }

        /// <summary>
        /// Index of the stored pixel that ends up at (uprightX, uprightY) of the upright image.
        /// Upright coordinates have their origin in the top-left corner.
        /// </summary>
        internal static int SourcePixelIndex(
            int uprightX,
            int uprightY,
            int sourceWidth,
            int sourceHeight,
            int rotation,
            bool sourceIsBottomUp)
        {
            int sourceX;
            int sourceYFromTop;

            switch (rotation)
            {
                case 90:
                    sourceX = uprightY;
                    sourceYFromTop = sourceHeight - 1 - uprightX;
                    break;
                case 180:
                    sourceX = sourceWidth - 1 - uprightX;
                    sourceYFromTop = sourceHeight - 1 - uprightY;
                    break;
                case 270:
                    sourceX = sourceWidth - 1 - uprightY;
                    sourceYFromTop = uprightX;
                    break;
                default:
                    sourceX = uprightX;
                    sourceYFromTop = uprightY;
                    break;
            }

            int row = sourceIsBottomUp ? sourceHeight - 1 - sourceYFromTop : sourceYFromTop;
            return row * sourceWidth + sourceX;
        }

        // Bilinear sampling positions with pixel centres aligned, the same convention as OpenCV's INTER_LINEAR.
        private static void Sample(int target, float scale, int sourceLength, out int first, out int second, out float weight)
        {
            float position = (target + 0.5f) * scale - 0.5f;
            int floor = (int)Math.Floor(position);
            weight = position - floor;

            if (floor < 0)
            {
                floor = 0;
                weight = 0f;
            }

            if (floor >= sourceLength - 1)
            {
                floor = sourceLength - 1;
                first = floor;
                second = floor;
                weight = 0f;
                return;
            }

            first = floor;
            second = floor + 1;
        }

        private static float Blend(byte p00, byte p01, byte p10, byte p11, float wx, float wy)
        {
            float top = p00 + (p01 - p00) * wx;
            float bottom = p10 + (p11 - p10) * wx;
            float value = top + (bottom - top) * wy;

            // The reference pipeline resizes an 8-bit image, so values are whole numbers.
            return (float)Math.Floor(value + 0.5f);
        }
    }
}
