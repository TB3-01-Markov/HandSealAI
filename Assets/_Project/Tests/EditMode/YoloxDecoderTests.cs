using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class YoloxDecoderTests
    {
        private const int RowLength = 5 + YoloxLabels.ClassCount;

        // A 416 x 416 input gives 52 x 52 anchors at stride 8, then 26 x 26 at stride 16, then 13 x 13 at stride 32.
        private const int Stride16Start = 52 * 52;
        private const int Stride32Start = 52 * 52 + 26 * 26;

        private static readonly LetterboxInfo Frame = new LetterboxInfo(832, 832, 416, 416, 0.5f);

        private static float[] EmptyOutput()
        {
            return new float[new YoloxDecoder().ExpectedLength];
        }

        private static void Plant(
            float[] output, int anchor, float x, float y, float logWidth, float logHeight,
            float objectness, int classId, float classScore)
        {
            int offset = anchor * RowLength;
            output[offset] = x;
            output[offset + 1] = y;
            output[offset + 2] = logWidth;
            output[offset + 3] = logHeight;
            output[offset + 4] = objectness;
            output[offset + 5 + classId] = classScore;
        }

        private static List<Detection> Decode(float[] output, float scoreThreshold = 0.3f, float nmsThreshold = 0.45f)
        {
            var results = new List<Detection>();
            new YoloxDecoder().Decode(output, Frame, scoreThreshold, nmsThreshold, results);
            return results;
        }

        [Test]
        public void AnchorCount_MatchesTheModel()
        {
            var decoder = new YoloxDecoder();

            Assert.AreEqual(3549, decoder.AnchorCount);
            Assert.AreEqual(3549 * 21, decoder.ExpectedLength);
        }

        [Test]
        public void EmptyOutput_GivesNoDetections()
        {
            Assert.AreEqual(0, Decode(EmptyOutput()).Count);
        }

        [Test]
        public void DecodesBox_FromGridCellAndStride()
        {
            float[] output = EmptyOutput();
            // Stride 16, cell (x = 10, y = 5): centre (10.5, 5.5) * 16 = (168, 88), size 64 x 32 in model pixels.
            int anchor = Stride16Start + 5 * 26 + 10;
            Plant(output, anchor, 0.5f, 0.5f, (float)Math.Log(4), (float)Math.Log(2), 0.9f, 6, 0.8f);

            List<Detection> detections = Decode(output);

            Assert.AreEqual(1, detections.Count);
            Detection detection = detections[0];
            Assert.AreEqual(SealType.Horse, detection.Seal);
            Assert.AreEqual(0.72f, detection.Score, 1e-5f);
            // Model pixels divided by the letterbox ratio 0.5.
            Assert.AreEqual(272f, detection.Box.XMin, 1e-2f);
            Assert.AreEqual(144f, detection.Box.YMin, 1e-2f);
            Assert.AreEqual(400f, detection.Box.XMax, 1e-2f);
            Assert.AreEqual(208f, detection.Box.YMax, 1e-2f);
        }

        [Test]
        public void UsesTheRightGrid_ForEachStride()
        {
            float[] output = EmptyOutput();
            Plant(output, 3 * 52 + 7, 0f, 0f, 0f, 0f, 0.9f, 0, 0.9f);
            Plant(output, Stride32Start + 12 * 13 + 12, 0f, 0f, 0f, 0f, 0.8f, 0, 0.9f);

            List<Detection> detections = Decode(output);

            Assert.AreEqual(2, detections.Count);
            // Stride 8, cell (7, 3): centre (56, 24), size 8, divided by 0.5.
            Assert.AreEqual(112f, detections[0].Box.CenterX, 1e-2f);
            Assert.AreEqual(48f, detections[0].Box.CenterY, 1e-2f);
            Assert.AreEqual(16f, detections[0].Box.Width, 1e-2f);
            // Stride 32, cell (12, 12): centre (384, 384), size 32, divided by 0.5.
            Assert.AreEqual(768f, detections[1].Box.CenterX, 1e-2f);
            Assert.AreEqual(768f, detections[1].Box.CenterY, 1e-2f);
            Assert.AreEqual(64f, detections[1].Box.Width, 1e-2f);
        }

        [Test]
        public void PicksTheClassWithTheHighestScore()
        {
            float[] output = EmptyOutput();
            int anchor = Stride16Start;
            Plant(output, anchor, 0.5f, 0.5f, 1f, 1f, 0.9f, 2, 0.4f);
            output[anchor * RowLength + 5 + 11] = 0.7f;

            List<Detection> detections = Decode(output);

            Assert.AreEqual(1, detections.Count);
            Assert.AreEqual(SealType.Boar, detections[0].Seal);
            Assert.AreEqual(0.63f, detections[0].Score, 1e-5f);
        }

        [Test]
        public void DropsDetections_AtOrBelowTheScoreThreshold()
        {
            float[] output = EmptyOutput();
            Plant(output, Stride16Start, 0.5f, 0.5f, 1f, 1f, 0.5f, 0, 0.5f);

            Assert.AreEqual(0, Decode(output, scoreThreshold: 0.3f).Count);
            Assert.AreEqual(1, Decode(output, scoreThreshold: 0.2f).Count);
        }

        [Test]
        public void Nms_KeepsTheBestOfOverlappingBoxes_AndDistantOnes()
        {
            float[] output = EmptyOutput();
            float logSize = (float)Math.Log(4);
            // Two neighbouring cells with almost the same box, even though the classes differ.
            Plant(output, Stride16Start + 5 * 26 + 10, 0.5f, 0.5f, logSize, logSize, 0.9f, 2, 0.9f);
            Plant(output, Stride16Start + 5 * 26 + 11, 0.0f, 0.5f, logSize, logSize, 0.9f, 3, 0.6f);
            // A box far away.
            Plant(output, Stride16Start + 20 * 26 + 20, 0.5f, 0.5f, logSize, logSize, 0.8f, 9, 0.5f);

            List<Detection> detections = Decode(output);

            Assert.AreEqual(2, detections.Count);
            Assert.AreEqual(SealType.Tiger, detections[0].Seal);
            Assert.AreEqual(SealType.Bird, detections[1].Seal);
            Assert.IsTrue(detections[0].Score > detections[1].Score);
        }

        [Test]
        public void ClampsBoxes_ToTheImage()
        {
            float[] output = EmptyOutput();
            // Stride 32, cell (0, 0): centre (16, 16), size 320: spills over the top-left corner.
            Plant(output, Stride32Start, 0.5f, 0.5f, (float)Math.Log(10), (float)Math.Log(10), 0.9f, 0, 0.9f);

            List<Detection> detections = Decode(output);

            Assert.AreEqual(0f, detections[0].Box.XMin, 1e-4f);
            Assert.AreEqual(0f, detections[0].Box.YMin, 1e-4f);
            Assert.AreEqual(352f, detections[0].Box.XMax, 1e-2f);
        }

        [Test]
        public void Decode_ClearsPreviousResults()
        {
            var results = new List<Detection> { new Detection(SealType.Rat, 1f, new BoundingBox(0, 0, 1, 1)) };

            new YoloxDecoder().Decode(EmptyOutput(), Frame, 0.3f, 0.45f, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Decode_RejectsOutputOfTheWrongLength()
        {
            var decoder = new YoloxDecoder();

            Assert.Throws<ArgumentException>(
                () => decoder.Decode(new float[100], Frame, 0.3f, 0.45f, new List<Detection>()));
        }

        [TestCase(0, SealType.Rat)]
        [TestCase(1, SealType.Ox)]
        [TestCase(6, SealType.Horse)]
        [TestCase(11, SealType.Boar)]
        [TestCase(12, SealType.Gassho)]
        [TestCase(13, SealType.Unknown)]
        [TestCase(14, SealType.Mizunoe)]
        [TestCase(15, SealType.Unknown)]
        [TestCase(-1, SealType.Unknown)]
        public void Labels_AreShiftedByOne(int classId, SealType expected)
        {
            Assert.AreEqual(expected, YoloxLabels.ToSeal(classId));
        }
    }
}
