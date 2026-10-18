using NUnit.Framework;

namespace Bowling.Contracts.Tests
{
    public class PinMaskExtensionsTests
    {
        [Test]
        public void AllCountsTen()
        {
            Assert.AreEqual(10, PinMask.All.Count());
        }

        [Test]
        public void NoneCountsZero()
        {
            Assert.AreEqual(0, PinMask.None.Count());
        }

        [TestCase(PinMask.P1)]
        [TestCase(PinMask.P2)]
        [TestCase(PinMask.P3)]
        [TestCase(PinMask.P4)]
        [TestCase(PinMask.P5)]
        [TestCase(PinMask.P6)]
        [TestCase(PinMask.P7)]
        [TestCase(PinMask.P8)]
        [TestCase(PinMask.P9)]
        [TestCase(PinMask.P10)]
        public void SinglePinCountsOne(PinMask pin)
        {
            Assert.AreEqual(1, pin.Count());
        }
    }
}
