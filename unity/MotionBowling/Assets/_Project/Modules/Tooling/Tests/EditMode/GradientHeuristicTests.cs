using System;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// The pure part of G-6: the fraction of opaque pixels that sit on a smooth ramp. Every case builds its
    /// pixels in code, so nothing here creates a Unity object.
    /// </summary>
    public class GradientHeuristicTests
    {
        const int MaxStep = 3;

        static Color32[] Fill(int width, int height, Func<int, int, Color32> pixelAt)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixelAt(x, y);
            return pixels;
        }

        static Color32 Gray(int value, byte alpha = 255) => new Color32((byte)value, (byte)value, (byte)value, alpha);

        [Test]
        public void SingleColorGivesZero()
        {
            Color32[] pixels = Fill(64, 64, (x, y) => new Color32(10, 120, 200, 255));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 64, 64, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void HorizontalRampOfOneStepPerPixelIsAlmostAllSmooth()
        {
            Color32[] pixels = Fill(256, 4, (x, y) => Gray(x));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 256, 4, MaxStep), Is.GreaterThan(0.9f));
        }

        [Test]
        public void VerticalRampIsFoundToo()
        {
            Color32[] pixels = Fill(4, 200, (x, y) => Gray(y));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 4, 200, MaxStep), Is.GreaterThan(0.9f));
        }

        [Test]
        public void TwoColorVerticalStripesGiveZero()
        {
            Color32[] pixels = Fill(64, 64, (x, y) => (x / 8) % 2 == 0 ? Gray(40) : Gray(200));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 64, 64, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void TwoFlatRegionsWithAOnePixelBlendColumnGiveZero()
        {
            // 100 | 160 | 220: every step between neighbours is 60, far above the ramp step.
            Color32[] pixels = Fill(33, 16, (x, y) => x < 16 ? Gray(100) : x == 16 ? Gray(160) : Gray(220));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 33, 16, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void PixelsThatAreNotFullyOpaqueAreIgnored()
        {
            Color32[] ramp = Fill(256, 4, (x, y) => Gray(x, 254));

            Assert.That(GradientHeuristic.SmoothStepFraction(ramp, 256, 4, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void OnlyOpaquePixelsCountInTheDenominator()
        {
            // Left half: an opaque ramp (smooth). Right half: translucent noise that must not dilute or add to it.
            Color32[] pixels = Fill(200, 4, (x, y) => x < 100 ? Gray(x) : Gray(x % 2 == 0 ? 10 : 250, 128));

            float fraction = GradientHeuristic.SmoothStepFraction(pixels, 200, 4, MaxStep);

            Assert.That(fraction, Is.GreaterThan(0.9f));
        }

        [Test]
        public void ATranslucentNeighbourIsNotJudged()
        {
            // An opaque pixel next to a translucent one that differs by a single step: the translucent pixel's
            // color is meaningless, so the opaque pixel is not smooth.
            Color32[] pixels = { Gray(100), Gray(101, 10) };

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 2, 1, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void StepIsJudgedOnAnyChannel()
        {
            Color32[] pixels = { new Color32(50, 50, 50, 255), new Color32(50, 50, 52, 255) };

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 2, 1, MaxStep), Is.EqualTo(1f));
        }

        [Test]
        public void StepEqualToMaxStepCountsAndOneMoreDoesNot()
        {
            Color32[] atLimit = { Gray(50), Gray(53) };
            Color32[] overLimit = { Gray(50), Gray(54) };

            Assert.That(GradientHeuristic.SmoothStepFraction(atLimit, 2, 1, MaxStep), Is.EqualTo(1f));
            Assert.That(GradientHeuristic.SmoothStepFraction(overLimit, 2, 1, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void DiagonalNeighboursAreNotNeighbours()
        {
            // Only the 4-neighbourhood counts: the diagonal pixel differs by 1 but no side neighbour does.
            Color32[] pixels =
            {
                Gray(100), Gray(200),
                Gray(200), Gray(101),
            };

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 2, 2, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void ARampPixelCountsOnceHoweverManyNeighboursQualify()
        {
            // A 3x3 block of value 100 with a single centre of 101: the centre has four qualifying neighbours but is
            // one pixel; its four side neighbours qualify through it. The corners touch only 100s.
            Color32[] pixels = Fill(3, 3, (x, y) => x == 1 && y == 1 ? Gray(101) : Gray(100));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 3, 3, MaxStep), Is.EqualTo(5f / 9f).Within(1e-6f));
        }

        [Test]
        public void NoOpaquePixelsGivesZero()
        {
            Color32[] pixels = Fill(4, 4, (x, y) => Gray(x, 0));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 4, 4, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void ASinglePixelHasNoNeighbours()
        {
            Assert.That(GradientHeuristic.SmoothStepFraction(new[] { Gray(5) }, 1, 1, MaxStep), Is.EqualTo(0f));
        }

        [Test]
        public void MaxStepZeroFindsNothing()
        {
            Color32[] pixels = Fill(256, 4, (x, y) => Gray(x));

            Assert.That(GradientHeuristic.SmoothStepFraction(pixels, 256, 4, 0), Is.EqualTo(0f));
        }

        [Test]
        public void PixelCountThatDoesNotMatchTheSizeThrows()
        {
            Assert.Throws<ArgumentException>(() => GradientHeuristic.SmoothStepFraction(new Color32[5], 2, 2, MaxStep));
        }

        [Test]
        public void NullPixelsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => GradientHeuristic.SmoothStepFraction(null, 2, 2, MaxStep));
        }
    }
}
