namespace HandSeals.Core
{
    /// <summary>
    /// How a camera frame was fitted into the square model input.
    /// Needed to convert boxes from model space back to image pixels.
    /// </summary>
    public readonly struct LetterboxInfo
    {
        public LetterboxInfo(int imageWidth, int imageHeight, int scaledWidth, int scaledHeight, float ratio)
        {
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            ScaledWidth = scaledWidth;
            ScaledHeight = scaledHeight;
            Ratio = ratio;
        }

        /// <summary>Width of the upright camera image (after rotation) in pixels.</summary>
        public int ImageWidth { get; }

        /// <summary>Height of the upright camera image (after rotation) in pixels.</summary>
        public int ImageHeight { get; }

        /// <summary>Width of the image inside the model input; the rest is padding.</summary>
        public int ScaledWidth { get; }

        /// <summary>Height of the image inside the model input; the rest is padding.</summary>
        public int ScaledHeight { get; }

        /// <summary>Model pixels per image pixel.</summary>
        public float Ratio { get; }
    }
}
