using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class RecognitionHintAdvisorTests
    {
        private RecognitionHintAdvisor _advisor;

        [SetUp]
        public void SetUp()
        {
            _advisor = new RecognitionHintAdvisor { DelaySeconds = 3.0 };
            _advisor.Reset(0.0);
        }

        [Test]
        public void NoHint_AtTheStart()
        {
            _advisor.Update(false, false, 1.0);

            Assert.AreEqual(RecognitionHint.None, _advisor.Current);
        }

        [Test]
        public void NothingDetected_ForTheDelay_AsksToShowTheHands()
        {
            _advisor.Update(false, false, 2.9);
            Assert.AreEqual(RecognitionHint.None, _advisor.Current);

            _advisor.Update(false, false, 3.0);
            Assert.AreEqual(RecognitionHint.ShowHands, _advisor.Current);
        }

        [Test]
        public void OnlyWeakDetections_ForTheDelay_AskToHoldSteady()
        {
            for (double t = 0.5; t <= 3.01; t += 0.5)
            {
                _advisor.Update(true, false, t);
            }

            Assert.AreEqual(RecognitionHint.HoldSteady, _advisor.Current);
        }

        [Test]
        public void ConfidentDetection_ClearsTheHint()
        {
            _advisor.Update(false, false, 4.0);
            Assert.AreEqual(RecognitionHint.ShowHands, _advisor.Current);

            _advisor.Update(true, true, 4.1);
            Assert.AreEqual(RecognitionHint.None, _advisor.Current);
        }

        [Test]
        public void WeakDetection_TurnsShowHandsIntoHoldSteady()
        {
            _advisor.Update(false, false, 4.0);
            _advisor.Update(true, false, 4.1);

            Assert.AreEqual(RecognitionHint.HoldSteady, _advisor.Current);
        }

        [Test]
        public void Reset_StartsCountingAgain()
        {
            _advisor.Update(false, false, 10.0);
            Assert.AreEqual(RecognitionHint.ShowHands, _advisor.Current);

            _advisor.Reset(10.0);
            Assert.AreEqual(RecognitionHint.None, _advisor.Current);
            _advisor.Update(false, false, 12.0);
            Assert.AreEqual(RecognitionHint.None, _advisor.Current);
        }

        [Test]
        public void FirstUpdate_WithoutReset_StartsTheClock()
        {
            var advisor = new RecognitionHintAdvisor();

            advisor.Update(false, false, 100.0);

            Assert.AreEqual(RecognitionHint.None, advisor.Current);
        }
    }
}
