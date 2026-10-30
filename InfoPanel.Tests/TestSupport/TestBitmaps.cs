using SkiaSharp;
using System;
using System.IO;

namespace InfoPanel.Tests.TestSupport
{
    internal static class TestBitmaps
    {
        public static SKBitmap Solid(int width, int height, SKColor color,
            SKColorType colorType = SKColorType.Bgra8888, SKAlphaType alphaType = SKAlphaType.Premul)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, colorType, alphaType));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(color);
            return bitmap;
        }

        /// <summary>Left half <paramref name="left"/>, right half <paramref name="right"/>.</summary>
        public static SKBitmap SplitVertical(int width, int height, SKColor left, SKColor right)
        {
            var bitmap = Solid(width, height, right);
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { Color = left };
            canvas.DrawRect(0, 0, width / 2f, height, paint);
            return bitmap;
        }

        /// <summary>Writes a PNG of the given size to a new temp file and returns its path.</summary>
        public static string WriteTempPng(int width, int height, SKColor color)
        {
            using var bitmap = Solid(width, height, color);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            var path = Path.Combine(Path.GetTempPath(), $"infopanel-test-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(path, data.ToArray());
            return path;
        }

        public static string WriteTempFile(string extension, string contents)
        {
            var path = Path.Combine(Path.GetTempPath(), $"infopanel-test-{Guid.NewGuid():N}{extension}");
            File.WriteAllText(path, contents);
            return path;
        }

        /// <summary>True when every channel is within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
        public static bool IsClose(SKColor actual, SKColor expected, int tolerance = 2)
        {
            return Math.Abs(actual.Red - expected.Red) <= tolerance
                && Math.Abs(actual.Green - expected.Green) <= tolerance
                && Math.Abs(actual.Blue - expected.Blue) <= tolerance
                && Math.Abs(actual.Alpha - expected.Alpha) <= tolerance;
        }
    }
}
