using NUnit.Framework;

namespace HandSeals.Core.Tests
{
    [TestFixture]
    public class ScoreCalculatorTests
    {
        private static readonly Jutsu Jutsu = new Jutsu("j", "J", new[] { SealType.Ox }, 10, 100);

        // timeLeft, combo, expected points
        [TestCase(0.0, 1, 100)]
        [TestCase(5.0, 1, 150)]
        [TestCase(10.0, 1, 200)]
        [TestCase(25.0, 1, 200)]
        [TestCase(-3.0, 1, 100)]
        [TestCase(0.0, 0, 100)]
        [TestCase(0.0, 2, 125)]
        [TestCase(0.0, 5, 200)]
        [TestCase(0.0, 9, 200)]
        [TestCase(10.0, 5, 400)]
        [TestCase(3.33, 1, 133)]
        public void Calculate_AppliesTimeBonusAndCombo(double timeLeft, int combo, int expected)
        {
            var calculator = new ScoreCalculator();

            Assert.AreEqual(expected, calculator.Calculate(Jutsu, timeLeft, combo));
        }
    }
}
