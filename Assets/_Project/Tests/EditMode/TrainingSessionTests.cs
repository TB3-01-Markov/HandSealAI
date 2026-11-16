using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class TrainingSessionTests
    {
        private static TrainingSession NewSession(int seed = 1)
        {
            return new TrainingSession(SealSets.Zodiac, new Random(seed));
        }

        private static SealType WrongSealFor(TrainingSession session)
        {
            return session.Target == SealType.Ox ? SealType.Dog : SealType.Ox;
        }

        [Test]
        public void Zodiac_HasTwelveDistinctSeals()
        {
            var unique = new HashSet<SealType>(SealSets.Zodiac);

            Assert.AreEqual(12, SealSets.Zodiac.Count);
            Assert.AreEqual(12, unique.Count);
            Assert.AreEqual(SealType.Rat, SealSets.Zodiac[0]);
            Assert.AreEqual(SealType.Boar, SealSets.Zodiac[11]);
        }

        [Test]
        public void StartsWithATargetFromThePool()
        {
            TrainingSession session = NewSession();

            Assert.IsTrue(new List<SealType>(SealSets.Zodiac).Contains(session.Target));
            Assert.AreEqual(0, session.CorrectCount);
            Assert.AreEqual(0, session.Mistakes);
        }

        [Test]
        public void CorrectSeal_CountsAndMovesToADifferentTarget()
        {
            TrainingSession session = NewSession();

            for (int i = 0; i < 200; i++)
            {
                SealType previous = session.Target;

                Assert.AreEqual(MatchResult.Correct, session.OnSealConfirmed(previous));
                Assert.IsTrue(session.Target != previous);
                Assert.IsTrue(session.Target.IsSeal());
            }

            Assert.AreEqual(200, session.CorrectCount);
            Assert.AreEqual(0, session.Mistakes);
        }

        [Test]
        public void EverySealOfThePool_ComesUpEventually()
        {
            TrainingSession session = NewSession(7);
            var seen = new HashSet<SealType>();

            for (int i = 0; i < 500; i++)
            {
                seen.Add(session.Target);
                session.OnSealConfirmed(session.Target);
            }

            Assert.AreEqual(12, seen.Count);
        }

        [Test]
        public void WrongSeal_CountsAMistake_AndKeepsTheTarget()
        {
            TrainingSession session = NewSession();
            SealType target = session.Target;

            Assert.AreEqual(MatchResult.Wrong, session.OnSealConfirmed(WrongSealFor(session)));

            Assert.AreEqual(1, session.Mistakes);
            Assert.AreEqual(0, session.CorrectCount);
            Assert.AreEqual(target, session.Target);
        }

        [TestCase(SealType.None)]
        [TestCase(SealType.Unknown)]
        public void NonSeals_AreIgnored(SealType seal)
        {
            TrainingSession session = NewSession();

            Assert.AreEqual(MatchResult.Ignored, session.OnSealConfirmed(seal));
            Assert.AreEqual(0, session.Mistakes);
        }

        [Test]
        public void Tracks_LastAndFastestAnswerTime()
        {
            TrainingSession session = NewSession();

            session.Tick(2.0);
            session.Tick(0.5);
            Assert.AreEqual(2.5, session.TargetElapsed, 1e-9);
            session.OnSealConfirmed(session.Target);
            Assert.AreEqual(2.5, session.LastSeconds, 1e-9);
            Assert.AreEqual(2.5, session.BestSeconds, 1e-9);
            Assert.AreEqual(0.0, session.TargetElapsed, 1e-9);

            session.Tick(1.0);
            session.OnSealConfirmed(session.Target);
            Assert.AreEqual(1.0, session.LastSeconds, 1e-9);
            Assert.AreEqual(1.0, session.BestSeconds, 1e-9);

            session.Tick(4.0);
            session.OnSealConfirmed(session.Target);
            Assert.AreEqual(4.0, session.LastSeconds, 1e-9);
            Assert.AreEqual(1.0, session.BestSeconds, 1e-9);
        }

        [Test]
        public void WrongSeal_DoesNotResetTheClock()
        {
            TrainingSession session = NewSession();
            session.Tick(1.5);

            session.OnSealConfirmed(WrongSealFor(session));

            Assert.AreEqual(1.5, session.TargetElapsed, 1e-9);
        }

        [Test]
        public void Skip_ChangesTheTarget_WithoutCounting()
        {
            TrainingSession session = NewSession();
            SealType previous = session.Target;
            session.Tick(3.0);

            session.Skip();

            Assert.IsTrue(session.Target != previous);
            Assert.AreEqual(0, session.CorrectCount);
            Assert.AreEqual(0.0, session.TargetElapsed, 1e-9);
        }

        [Test]
        public void SingleSealPool_KeepsTheSameTarget()
        {
            var session = new TrainingSession(new[] { SealType.Tiger, SealType.Tiger }, new Random(1));

            Assert.AreEqual(MatchResult.Correct, session.OnSealConfirmed(SealType.Tiger));
            Assert.AreEqual(SealType.Tiger, session.Target);
            Assert.AreEqual(1, session.CorrectCount);
        }

        [Test]
        public void Constructor_RejectsAnEmptyPool()
        {
            Assert.Throws<ArgumentException>(() => new TrainingSession(new SealType[0]));
        }

        [Test]
        public void Constructor_RejectsNonSeals()
        {
            Assert.Throws<ArgumentException>(() => new TrainingSession(new[] { SealType.Ox, SealType.Unknown }));
        }

        [Test]
        public void Tick_RejectsNegativeTime()
        {
            TrainingSession session = NewSession();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(-1));
        }
    }
}
