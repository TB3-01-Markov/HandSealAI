using System;
using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class JutsuTests
    {
        internal static Jutsu Sample(params SealType[] sequence)
        {
            return new Jutsu("sample", "Sample", sequence, 10, 100);
        }

        [Test]
        public void Create_ParsesSealNames_IgnoringCaseAndSpaces()
        {
            Jutsu jutsu = Jutsu.Create("ember", "Ember Spark", new[] { "Ox", "tiger", " BIRD " }, 8, 100);

            Assert.AreEqual(3, jutsu.Sequence.Count);
            Assert.AreEqual(SealType.Ox, jutsu.Sequence[0]);
            Assert.AreEqual(SealType.Tiger, jutsu.Sequence[1]);
            Assert.AreEqual(SealType.Bird, jutsu.Sequence[2]);
            Assert.AreEqual(8.0, jutsu.TimeLimitSeconds, 1e-9);
            Assert.AreEqual(100, jutsu.BaseScore);
        }

        [TestCase("Dragonfly")]
        [TestCase("")]
        [TestCase("3")]
        [TestCase("None")]
        [TestCase("Unknown")]
        public void Create_RejectsInvalidSealNames(string sealName)
        {
            Assert.Throws<ArgumentException>(() => Jutsu.Create("x", "X", new[] { "Ox", sealName }, 8, 100));
        }

        [Test]
        public void Constructor_RejectsEmptySequence()
        {
            Assert.Throws<ArgumentException>(() => new Jutsu("x", "X", new SealType[0], 8, 100));
        }

        [Test]
        public void Constructor_RejectsNonPositiveTimeLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Jutsu("x", "X", new[] { SealType.Ox }, 0, 100));
        }

        [Test]
        public void Constructor_RejectsMissingId()
        {
            Assert.Throws<ArgumentException>(() => new Jutsu(" ", "X", new[] { SealType.Ox }, 5, 100));
        }

        [Test]
        public void Sequence_IsACopyOfTheInput()
        {
            var seals = new[] { SealType.Ox, SealType.Dog };
            var jutsu = new Jutsu("x", "X", seals, 5, 100);

            seals[0] = SealType.Rat;

            Assert.AreEqual(SealType.Ox, jutsu.Sequence[0]);
        }
    }
}
