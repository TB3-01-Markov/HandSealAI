using System;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class LetterboxPreprocessorTests
    {
        private const float Pad = LetterboxPreprocessor.PadValue;

        // Builds an RGBA buffer where every pixel has a distinct red value: 10 * row + column.
        private static byte[] NumberedPixels(int width, int height)
        {
            var pixels = new byte[width * height * 4];
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int i = (row * width + column) * 4;
                    pixels[i] = (byte)(10 * row + column);
                    pixels[i + 1] = 100;
                    pixels[i + 2] = 200;
                    pixels[i + 3] = 255;
                }
            }

            return pixels;
        }

        // Red plane (the third one: the model input is BGR) at an upright position.
        private static float Red(LetterboxPreprocessor preprocessor, int x, int y)
        {
            int size = preprocessor.InputSize;
            return preprocessor.Buffer[2 * size * size + y * size + x];
        }

        [Test]
        public void SameSizeImage_IsCopied_AsPlanarBgr()
        {
            var preprocessor = new LetterboxPreprocessor(2);
            byte[] pixels =
            {
                1, 2, 3, 255, 4, 5, 6, 255,
                7, 8, 9, 255, 10, 11, 12, 255
            };

            LetterboxInfo info = preprocessor.Fill(pixels, 2, 2, 0, false);

            CollectionAssert.AreEqual(
                new float[] { 3, 6, 9, 12, 2, 5, 8, 11, 1, 4, 7, 10 },
                preprocessor.Buffer);
            Assert.AreEqual(1f, info.Ratio, 1e-6f);
            Assert.AreEqual(2, info.ImageWidth);
            Assert.AreEqual(2, info.ImageHeight);
        }

        [Test]
        public void WideImage_IsPlacedAtTheTop_AndPaddedBelow()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            LetterboxInfo info = preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 0, false);

            Assert.AreEqual(4, info.ScaledWidth);
            Assert.AreEqual(2, info.ScaledHeight);
            Assert.AreEqual(0f, Red(preprocessor, 0, 0), 1e-6f);
            Assert.AreEqual(13f, Red(preprocessor, 3, 1), 1e-6f);
            Assert.AreEqual(Pad, Red(preprocessor, 0, 2), 1e-6f);
            Assert.AreEqual(Pad, Red(preprocessor, 3, 3), 1e-6f);
            // Padding fills every channel.
            Assert.AreEqual(Pad, preprocessor.Buffer[3 * 4 + 3], 1e-6f);
        }

        [Test]
        public void TallImage_IsPlacedAtTheLeft_AndPaddedOnTheRight()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            LetterboxInfo info = preprocessor.Fill(NumberedPixels(2, 4), 2, 4, 0, false);

            Assert.AreEqual(2, info.ScaledWidth);
            Assert.AreEqual(4, info.ScaledHeight);
            Assert.AreEqual(31f, Red(preprocessor, 1, 3), 1e-6f);
            Assert.AreEqual(Pad, Red(preprocessor, 2, 0), 1e-6f);
        }

        [Test]
        public void BottomUpSource_IsFlippedVertically()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 0, true);

            // The first stored row is the bottom of the picture.
            Assert.AreEqual(10f, Red(preprocessor, 0, 0), 1e-6f);
            Assert.AreEqual(0f, Red(preprocessor, 0, 1), 1e-6f);
            Assert.AreEqual(3f, Red(preprocessor, 3, 1), 1e-6f);
        }

        [Test]
        public void Rotation90_TurnsTheImageClockwise()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            // Stored 4 wide, 2 high; upright 2 wide, 4 high.
            LetterboxInfo info = preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 90, false);

            Assert.AreEqual(2, info.ImageWidth);
            Assert.AreEqual(4, info.ImageHeight);
            // Clockwise: the stored top-left corner moves to the top-right, the stored bottom-left to the top-left.
            Assert.AreEqual(0f, Red(preprocessor, 1, 0), 1e-6f);
            Assert.AreEqual(10f, Red(preprocessor, 0, 0), 1e-6f);
            Assert.AreEqual(3f, Red(preprocessor, 1, 3), 1e-6f);
            Assert.AreEqual(13f, Red(preprocessor, 0, 3), 1e-6f);
        }

        [Test]
        public void Rotation180_TurnsTheImageUpsideDown()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 180, false);

            Assert.AreEqual(13f, Red(preprocessor, 0, 0), 1e-6f);
            Assert.AreEqual(0f, Red(preprocessor, 3, 1), 1e-6f);
        }

        [Test]
        public void Rotation270_TurnsTheImageCounterClockwise()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            LetterboxInfo info = preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 270, false);

            Assert.AreEqual(2, info.ImageWidth);
            Assert.AreEqual(4, info.ImageHeight);
            // Counter-clockwise: the stored top-left corner moves to the bottom-left, the top-right to the top-left.
            Assert.AreEqual(0f, Red(preprocessor, 0, 3), 1e-6f);
            Assert.AreEqual(3f, Red(preprocessor, 0, 0), 1e-6f);
            Assert.AreEqual(13f, Red(preprocessor, 1, 0), 1e-6f);
        }

        [Test]
        public void NegativeRotation_IsNormalised()
        {
            var a = new LetterboxPreprocessor(4);
            var b = new LetterboxPreprocessor(4);

            a.Fill(NumberedPixels(4, 2), 4, 2, -90, false);
            b.Fill(NumberedPixels(4, 2), 4, 2, 270, false);

            CollectionAssert.AreEqual(b.Buffer, a.Buffer);
        }

        [Test]
        public void LargerImage_IsScaledDown_WithTheReportedRatio()
        {
            var preprocessor = new LetterboxPreprocessor(4);
            var pixels = new byte[8 * 16 * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 50;
                pixels[i + 1] = 60;
                pixels[i + 2] = 70;
                pixels[i + 3] = 255;
            }

            LetterboxInfo info = preprocessor.Fill(pixels, 8, 16, 0, false);

            Assert.AreEqual(0.25f, info.Ratio, 1e-6f);
            Assert.AreEqual(2, info.ScaledWidth);
            Assert.AreEqual(4, info.ScaledHeight);
            Assert.AreEqual(50f, Red(preprocessor, 1, 3), 1e-6f);
            Assert.AreEqual(70f, preprocessor.Buffer[0], 1e-6f);
            Assert.AreEqual(Pad, Red(preprocessor, 2, 3), 1e-6f);
        }

        [Test]
        public void Downscaling_AveragesNeighbouringPixels()
        {
            var preprocessor = new LetterboxPreprocessor(1);
            byte[] pixels =
            {
                0, 0, 0, 255, 100, 0, 0, 255,
                100, 0, 0, 255, 200, 0, 0, 255
            };

            preprocessor.Fill(pixels, 2, 2, 0, false);

            Assert.AreEqual(100f, preprocessor.Buffer[2], 1e-6f);
        }

        [Test]
        public void Fill_OverwritesThePreviousFrame()
        {
            var preprocessor = new LetterboxPreprocessor(4);
            preprocessor.Fill(NumberedPixels(2, 4), 2, 4, 0, false);

            preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 0, false);

            Assert.AreEqual(Pad, Red(preprocessor, 0, 3), 1e-6f);
        }

        [Test]
        public void Fill_RejectsRotationsThatAreNotRightAngles()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            Assert.Throws<ArgumentException>(() => preprocessor.Fill(NumberedPixels(4, 2), 4, 2, 45, false));
        }

        [Test]
        public void Fill_RejectsABufferThatIsTooSmall()
        {
            var preprocessor = new LetterboxPreprocessor(4);

            Assert.Throws<ArgumentException>(() => preprocessor.Fill(new byte[10], 4, 2, 0, false));
        }
    }
}
