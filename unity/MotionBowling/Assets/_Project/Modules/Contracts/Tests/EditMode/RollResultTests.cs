using NUnit.Framework;

namespace Bowling.Contracts.Tests
{
    public class RollResultTests
    {
        private const PinMask SevenTen = PinMask.P7 | PinMask.P10;

        [Test]
        public void FullRackToEmptyKnocksTen()
        {
            var result = new RollResult(PinMask.All, PinMask.None, false, RollEndReason.Settled);

            Assert.AreEqual(10, result.PinsKnocked);
        }

        [Test]
        public void FullRackToSevenTenKnocksEight()
        {
            var result = new RollResult(PinMask.All, SevenTen, false, RollEndReason.Settled);

            Assert.AreEqual(8, result.PinsKnocked);
        }

        [Test]
        public void SevenTenToTenKnocksOne()
        {
            var result = new RollResult(SevenTen, PinMask.P10, false, RollEndReason.Settled);

            Assert.AreEqual(1, result.PinsKnocked);
        }
    }
}
