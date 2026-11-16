using System.Collections.Generic;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class SealStabilizerTests
    {
        private SealStabilizer _stabilizer;
        private List<SealType> _confirmed;

        [SetUp]
        public void SetUp()
        {
            _stabilizer = new SealStabilizer { HoldSeconds = 0.4, MinScore = 0.6f, GraceSeconds = 0.15 };
            _confirmed = new List<SealType>();
            _stabilizer.SealConfirmed += seal => _confirmed.Add(seal);
        }

        [Test]
        public void Confirms_AfterHoldTime_NotBefore()
        {
            _stabilizer.Update(SealType.Tiger, 0.9f, 0.0);
            _stabilizer.Update(SealType.Tiger, 0.9f, 0.3);
            Assert.AreEqual(0, _confirmed.Count);

            _stabilizer.Update(SealType.Tiger, 0.9f, 0.4);
            Assert.AreEqual(1, _confirmed.Count);
            Assert.AreEqual(SealType.Tiger, _confirmed[0]);
            Assert.IsTrue(_stabilizer.IsConfirmed);
        }

        [Test]
        public void HeldSeal_IsConfirmedOnlyOnce()
        {
            for (int i = 0; i <= 20; i++)
            {
                _stabilizer.Update(SealType.Ox, 0.8f, i * 0.1);
            }

            Assert.AreEqual(1, _confirmed.Count);
        }

        [Test]
        public void SameSeal_ConfirmsAgain_AfterHandsAreReleased()
        {
            _stabilizer.Update(SealType.Ox, 0.8f, 0.0);
            _stabilizer.Update(SealType.Ox, 0.8f, 0.5);
            _stabilizer.Update(SealType.None, 0f, 0.6);
            _stabilizer.Update(SealType.None, 0f, 0.8);
            Assert.AreEqual(SealType.None, _stabilizer.Candidate);

            _stabilizer.Update(SealType.Ox, 0.8f, 1.0);
            _stabilizer.Update(SealType.Ox, 0.8f, 1.5);

            Assert.AreEqual(2, _confirmed.Count);
        }

        [Test]
        public void ShortDetectionGap_DoesNotResetTheHold()
        {
            _stabilizer.Update(SealType.Bird, 0.9f, 0.0);
            _stabilizer.Update(SealType.Bird, 0.9f, 0.2);
            _stabilizer.Update(SealType.None, 0f, 0.3);
            _stabilizer.Update(SealType.Bird, 0.9f, 0.4);

            Assert.AreEqual(1, _confirmed.Count);
        }

        [Test]
        public void LongDetectionGap_RestartsTheHold()
        {
            _stabilizer.Update(SealType.Bird, 0.9f, 0.0);
            _stabilizer.Update(SealType.Bird, 0.9f, 0.2);
            _stabilizer.Update(SealType.None, 0f, 0.4);
            _stabilizer.Update(SealType.Bird, 0.9f, 0.5);
            Assert.AreEqual(0, _confirmed.Count);

            _stabilizer.Update(SealType.Bird, 0.9f, 0.9);
            Assert.AreEqual(1, _confirmed.Count);
        }

        [Test]
        public void LowScore_IsTreatedAsNothing()
        {
            _stabilizer.Update(SealType.Dog, 0.5f, 0.0);
            _stabilizer.Update(SealType.Dog, 0.5f, 1.0);

            Assert.AreEqual(0, _confirmed.Count);
            Assert.AreEqual(SealType.None, _stabilizer.Candidate);
        }

        [Test]
        public void UnknownGesture_IsNeverConfirmed()
        {
            _stabilizer.Update(SealType.Unknown, 0.99f, 0.0);
            _stabilizer.Update(SealType.Unknown, 0.99f, 1.0);

            Assert.AreEqual(0, _confirmed.Count);
        }

        [Test]
        public void ChangingSeal_RestartsTheHold()
        {
            _stabilizer.Update(SealType.Rat, 0.9f, 0.0);
            _stabilizer.Update(SealType.Rat, 0.9f, 0.3);
            _stabilizer.Update(SealType.Boar, 0.9f, 0.35);
            _stabilizer.Update(SealType.Boar, 0.9f, 0.5);
            Assert.AreEqual(0, _confirmed.Count);

            _stabilizer.Update(SealType.Boar, 0.9f, 0.75);
            Assert.AreEqual(1, _confirmed.Count);
            Assert.AreEqual(SealType.Boar, _confirmed[0]);
        }

        [Test]
        public void HoldProgress_GrowsFromZeroToOne()
        {
            Assert.AreEqual(0.0, _stabilizer.GetHoldProgress(0.0), 1e-9);

            _stabilizer.Update(SealType.Snake, 0.9f, 1.0);
            Assert.AreEqual(0.0, _stabilizer.GetHoldProgress(1.0), 1e-9);
            Assert.AreEqual(0.5, _stabilizer.GetHoldProgress(1.2), 1e-9);

            _stabilizer.Update(SealType.Snake, 0.9f, 1.4);
            Assert.AreEqual(1.0, _stabilizer.GetHoldProgress(1.4), 1e-9);
            Assert.AreEqual(1.0, _stabilizer.GetHoldProgress(5.0), 1e-9);
        }

        [Test]
        public void ZeroHoldTime_ConfirmsImmediately()
        {
            _stabilizer.HoldSeconds = 0;
            _stabilizer.Update(SealType.Horse, 0.9f, 3.0);

            Assert.AreEqual(1, _confirmed.Count);
        }

        [Test]
        public void Reset_ClearsTheCandidate()
        {
            _stabilizer.Update(SealType.Ram, 0.9f, 0.0);
            _stabilizer.Reset();

            Assert.AreEqual(SealType.None, _stabilizer.Candidate);
            Assert.IsFalse(_stabilizer.IsConfirmed);
        }
    }
}
