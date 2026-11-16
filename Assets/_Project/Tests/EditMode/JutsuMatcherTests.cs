using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class JutsuMatcherTests
    {
        [Test]
        public void CorrectSeals_AdvanceAndComplete()
        {
            var matcher = new JutsuMatcher(JutsuTests.Sample(SealType.Ox, SealType.Tiger, SealType.Bird));

            Assert.AreEqual(SealType.Ox, matcher.Expected);
            Assert.AreEqual(MatchResult.Correct, matcher.Apply(SealType.Ox));
            Assert.AreEqual(MatchResult.Correct, matcher.Apply(SealType.Tiger));
            Assert.AreEqual(2, matcher.Progress);
            Assert.AreEqual(MatchResult.Completed, matcher.Apply(SealType.Bird));
            Assert.IsTrue(matcher.IsCompleted);
            Assert.AreEqual(SealType.None, matcher.Expected);
        }

        [Test]
        public void WrongSeal_ResetsProgress()
        {
            var matcher = new JutsuMatcher(JutsuTests.Sample(SealType.Ox, SealType.Tiger, SealType.Bird));
            matcher.Apply(SealType.Ox);
            matcher.Apply(SealType.Tiger);

            Assert.AreEqual(MatchResult.Wrong, matcher.Apply(SealType.Dog));
            Assert.AreEqual(0, matcher.Progress);
            Assert.AreEqual(SealType.Ox, matcher.Expected);
        }

        [Test]
        public void WrongSeal_ThatOpensTheSequence_CountsAsFirstStep()
        {
            var matcher = new JutsuMatcher(JutsuTests.Sample(SealType.Ox, SealType.Tiger, SealType.Bird));
            matcher.Apply(SealType.Ox);
            matcher.Apply(SealType.Tiger);

            Assert.AreEqual(MatchResult.Wrong, matcher.Apply(SealType.Ox));
            Assert.AreEqual(1, matcher.Progress);
            Assert.AreEqual(SealType.Tiger, matcher.Expected);
        }

        [TestCase(SealType.None)]
        [TestCase(SealType.Unknown)]
        public void NonSeals_AreIgnored(SealType seal)
        {
            var matcher = new JutsuMatcher(JutsuTests.Sample(SealType.Ox, SealType.Tiger));
            matcher.Apply(SealType.Ox);

            Assert.AreEqual(MatchResult.Ignored, matcher.Apply(seal));
            Assert.AreEqual(1, matcher.Progress);
        }

        [Test]
        public void AfterCompletion_FurtherSealsAreIgnored()
        {
            var matcher = new JutsuMatcher(JutsuTests.Sample(SealType.Ox));

            Assert.AreEqual(MatchResult.Completed, matcher.Apply(SealType.Ox));
            Assert.AreEqual(MatchResult.Ignored, matcher.Apply(SealType.Ox));
        }

        [Test]
        public void Reset_StartsOver()
        {
            var matcher = new JutsuMatcher(JutsuTests.Sample(SealType.Ox, SealType.Tiger));
            matcher.Apply(SealType.Ox);

            matcher.Reset();

            Assert.AreEqual(0, matcher.Progress);
        }
    }
}
