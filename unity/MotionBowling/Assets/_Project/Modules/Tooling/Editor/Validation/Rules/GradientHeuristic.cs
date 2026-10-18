using System;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The pixel test behind G-6 (foundation spec, section 8.1). A smooth color ramp is a run of pixels that each
    /// differ from the next by a tiny non-zero step, which flat art with hard edges never has. Pure: it reads a
    /// pixel array and creates no Unity object, so the tests build their pixels in code.
    /// </summary>
    public static class GradientHeuristic
    {
        const byte Opaque = 255;

        /// <summary>
        /// The fraction (0 to 1) of fully opaque pixels (<c>a == 255</c>) that have at least one 4-neighbour
        /// (left, right, above, below) differing by <c>0 &lt; |delta| &lt;= maxStep</c> on any of r, g, b.
        /// Pixels that are not fully opaque are ignored, and so are neighbours that are not: the color of a
        /// translucent pixel is not what the player sees. 0 when no pixel is opaque.
        /// </summary>
        /// <param name="pixels">Row-major pixels; <c>width * height</c> of them.</param>
        public static float SmoothStepFraction(Color32[] pixels, int width, int height, int maxStep)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (width < 0 || height < 0 || pixels.Length != width * height)
                throw new ArgumentException($"{pixels.Length} pixels do not make a {width}x{height} image.", nameof(pixels));

            int opaque = 0;
            int smooth = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 pixel = pixels[index];
                    if (pixel.a != Opaque) continue;

                    opaque++;
                    if ((x > 0 && IsSmoothStep(pixel, pixels[index - 1], maxStep))
                        || (x < width - 1 && IsSmoothStep(pixel, pixels[index + 1], maxStep))
                        || (y > 0 && IsSmoothStep(pixel, pixels[index - width], maxStep))
                        || (y < height - 1 && IsSmoothStep(pixel, pixels[index + width], maxStep)))
                    {
                        smooth++;
                    }
                }
            }
            return opaque == 0 ? 0f : (float)smooth / opaque;
        }

        static bool IsSmoothStep(Color32 pixel, Color32 neighbour, int maxStep)
        {
            return neighbour.a == Opaque
                && (IsStep(pixel.r, neighbour.r, maxStep) || IsStep(pixel.g, neighbour.g, maxStep) || IsStep(pixel.b, neighbour.b, maxStep));
        }

        static bool IsStep(byte a, byte b, int maxStep)
        {
            int delta = Math.Abs(a - b);
            return delta > 0 && delta <= maxStep;
        }
    }
}
