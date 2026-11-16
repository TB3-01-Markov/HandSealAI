using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class GameSessionTests
    {
        private static readonly Jutsu Ember = new Jutsu("ember", "Ember", new[] { SealType.Ox, SealType.Tiger }, 10, 100);

        private static GameSession StartedSession(double countdown = 0)
        {
            var session = new GameSession(new ScoreCalculator(), countdown);
            session.Start(Ember);
            return session;
        }

        [Test]
        public void NewSession_IsIdle()
        {
            var session = new GameSession();

            Assert.AreEqual(SessionState.Idle, session.State);
            Assert.IsNull(session.Current);
            Assert.AreEqual(0, session.Progress);
        }

        [Test]
        public void Start_RunsCountdown_ThenWaitsForSeals()
        {
            GameSession session = StartedSession(countdown: 3);
            Assert.AreEqual(SessionState.Countdown, session.State);

            session.Tick(2.0);
            Assert.AreEqual(SessionState.Countdown, session.State);
            Assert.AreEqual(1.0, session.CountdownLeft, 1e-9);

            session.Tick(1.0);
            Assert.AreEqual(SessionState.WaitingForSeal, session.State);
            Assert.AreEqual(10.0, session.TimeLeft, 1e-9);
        }

        [Test]
        public void Seals_DuringCountdown_AreIgnored()
        {
            GameSession session = StartedSession(countdown: 3);

            Assert.AreEqual(MatchResult.Ignored, session.OnSealConfirmed(SealType.Ox));
            Assert.AreEqual(0, session.Progress);
        }

        [Test]
        public void CompletingTheSequence_ScoresAndCompletes()
        {
            GameSession session = StartedSession();
            session.Tick(5.0);

            Assert.AreEqual(MatchResult.Correct, session.OnSealConfirmed(SealType.Ox));
            Assert.AreEqual(MatchResult.Completed, session.OnSealConfirmed(SealType.Tiger));

            Assert.AreEqual(SessionState.Completed, session.State);
            Assert.AreEqual(5.0, session.Elapsed, 1e-9);
            Assert.AreEqual(1, session.Combo);
            Assert.AreEqual(150, session.LastScore);
            Assert.AreEqual(150, session.TotalScore);
        }

        [Test]
        public void Timeout_FailsAndBreaksTheCombo()
        {
            GameSession session = StartedSession();
            session.OnSealConfirmed(SealType.Ox);
            session.OnSealConfirmed(SealType.Tiger);
            session.Start(Ember);

            session.Tick(10.5);

            Assert.AreEqual(SessionState.Failed, session.State);
            Assert.AreEqual(0.0, session.TimeLeft, 1e-9);
            Assert.AreEqual(0, session.Combo);
        }

        [Test]
        public void WrongSeal_CountsAsMistake_AndBreaksTheCombo()
        {
            GameSession session = StartedSession();
            session.OnSealConfirmed(SealType.Ox);
            session.OnSealConfirmed(SealType.Tiger);
            Assert.AreEqual(1, session.Combo);

            session.Start(Ember);
            session.OnSealConfirmed(SealType.Dog);
            Assert.AreEqual(1, session.Mistakes);
            session.OnSealConfirmed(SealType.Ox);
            session.OnSealConfirmed(SealType.Tiger);

            Assert.AreEqual(SessionState.Completed, session.State);
            Assert.AreEqual(0, session.Combo);
        }

        [Test]
        public void CleanStreak_GrowsTheCombo_AndTheScore()
        {
            GameSession session = StartedSession();
            session.OnSealConfirmed(SealType.Ox);
            session.OnSealConfirmed(SealType.Tiger);
            int first = session.LastScore;

            session.Start(Ember);
            session.OnSealConfirmed(SealType.Ox);
            session.OnSealConfirmed(SealType.Tiger);

            Assert.AreEqual(2, session.Combo);
            Assert.AreEqual(200, first);
            Assert.AreEqual(250, session.LastScore);
            Assert.AreEqual(450, session.TotalScore);
        }

        [Test]
        public void Pause_StopsTheTimer_AndResumeContinues()
        {
            GameSession session = StartedSession();
            session.Tick(2.0);

            session.Pause();
            Assert.AreEqual(SessionState.Paused, session.State);
            session.Tick(100.0);
            Assert.AreEqual(8.0, session.TimeLeft, 1e-9);
            Assert.AreEqual(MatchResult.Ignored, session.OnSealConfirmed(SealType.Ox));

            session.Resume();
            Assert.AreEqual(SessionState.WaitingForSeal, session.State);
            session.Tick(1.0);
            Assert.AreEqual(7.0, session.TimeLeft, 1e-9);
        }

        [Test]
        public void Pause_DuringCountdown_ResumesTheCountdown()
        {
            GameSession session = StartedSession(countdown: 3);

            session.Pause();
            session.Resume();

            Assert.AreEqual(SessionState.Countdown, session.State);
        }

        [Test]
        public void Abort_ReturnsToIdle()
        {
            GameSession session = StartedSession();

            session.Abort();

            Assert.AreEqual(SessionState.Idle, session.State);
        }

        [Test]
        public void Events_ReportStatesAndSeals()
        {
            var session = new GameSession(new ScoreCalculator(), 0);
            var states = new List<SessionState>();
            var seals = new List<MatchResult>();
            session.StateChanged += states.Add;
            session.SealApplied += (seal, result) => seals.Add(result);

            session.Start(Ember);
            session.OnSealConfirmed(SealType.Dog);
            session.OnSealConfirmed(SealType.Ox);
            session.OnSealConfirmed(SealType.Tiger);

            CollectionAssert.AreEqual(new[] { SessionState.WaitingForSeal, SessionState.Completed }, states);
            CollectionAssert.AreEqual(new[] { MatchResult.Wrong, MatchResult.Correct, MatchResult.Completed }, seals);
        }

        [Test]
        public void Start_RejectsNull()
        {
            var session = new GameSession();

            Assert.Throws<ArgumentNullException>(() => session.Start(null));
        }

        [Test]
        public void Tick_RejectsNegativeTime()
        {
            GameSession session = StartedSession();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(-0.1));
        }
    }
}
