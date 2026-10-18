using System;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Contracts.Tests
{
    public class LaneSpecTests
    {
        private const float Tolerance = 1e-4f;

        [TestCase(1, 0f, 18.288f)]
        [TestCase(2, -0.1524f, 18.55196f)]
        [TestCase(3, 0.1524f, 18.55196f)]
        [TestCase(4, -0.3048f, 18.81592f)]
        [TestCase(5, 0f, 18.81592f)]
        [TestCase(6, 0.3048f, 18.81592f)]
        [TestCase(7, -0.4572f, 19.07988f)]
        [TestCase(8, -0.1524f, 19.07988f)]
        [TestCase(9, 0.1524f, 19.07988f)]
        [TestCase(10, 0.4572f, 19.07988f)]
        public void PinSpotMatchesTable(int pin, float x, float z)
        {
            Vector3 spot = LaneSpec.PinSpot(pin);

            Assert.AreEqual(x, spot.x, Tolerance);
            Assert.AreEqual(0f, spot.y);
            Assert.AreEqual(z, spot.z, Tolerance);
        }

        [TestCase(0)]
        [TestCase(11)]
        public void PinSpotOutsideRangeThrows(int pin)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LaneSpec.PinSpot(pin));
        }
    }
}
