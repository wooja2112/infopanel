using InfoPanel.Extensions;
using InfoPanel.Tests.TestSupport;
using InfoPanel.ViewModels;
using SkiaSharp;
using Xunit;

namespace InfoPanel.Tests.Extensions
{
    public class SKBitmapExtensionsTests
    {
        [Fact]
        public void EnsureBitmapSize_ReturnsSameInstance_WhenNoChangeNeeded()
        {
            using var source = TestBitmaps.Solid(100, 50, SKColors.Red);

            var result = SKBitmapExtensions.EnsureBitmapSize(source, 100, 50);

            Assert.Same(source, result);
        }

        [Fact]
        public void EnsureBitmapSize_PreservesColorType()
        {
            using var source = TestBitmaps.Solid(100, 50, SKColors.Red, SKColorType.Rgb565, SKAlphaType.Opaque);

            using var result = SKBitmapExtensions.EnsureBitmapSize(source, 200, 100);

            Assert.Equal(SKColorType.Rgb565, result.ColorType);
            Assert.Equal(200, result.Width);
            Assert.Equal(100, result.Height);
        }

        [Fact]
        public void EnsureBitmapSize_Rotate90_SwapsDimensionsAndRotatesClockwise()
        {
            // Left half red, right half blue. Rotated 90° clockwise the left edge becomes the top.
            using var source = TestBitmaps.SplitVertical(100, 50, SKColors.Red, SKColors.Blue);

            using var result = SKBitmapExtensions.EnsureBitmapSize(source, 50, 100, LCD_ROTATION.Rotate90FlipNone);

            Assert.Equal(50, result.Width);
            Assert.Equal(100, result.Height);
            Assert.True(TestBitmaps.IsClose(result.GetPixel(25, 10), SKColors.Red), $"top was {result.GetPixel(25, 10)}");
            Assert.True(TestBitmaps.IsClose(result.GetPixel(25, 90), SKColors.Blue), $"bottom was {result.GetPixel(25, 90)}");
        }

        [Fact]
        public void EnsureBitmapSize_Rotate180_FlipsBothAxes()
        {
            using var source = TestBitmaps.SplitVertical(100, 50, SKColors.Red, SKColors.Blue);

            using var result = SKBitmapExtensions.EnsureBitmapSize(source, 100, 50, LCD_ROTATION.Rotate180FlipNone);

            Assert.True(TestBitmaps.IsClose(result.GetPixel(10, 25), SKColors.Blue));
            Assert.True(TestBitmaps.IsClose(result.GetPixel(90, 25), SKColors.Red));
        }

        [Fact]
        public void EnsureBitmapSize_Letterboxes_WhenAspectRatioDiffers()
        {
            using var source = TestBitmaps.Solid(100, 100, SKColors.Red);

            using var result = SKBitmapExtensions.EnsureBitmapSize(source, 200, 100);

            // Scaled to 100x100 and centred horizontally: x 50..150 is image, the sides are empty.
            Assert.Equal(0, result.GetPixel(10, 50).Alpha);
            Assert.Equal(0, result.GetPixel(190, 50).Alpha);
            Assert.True(TestBitmaps.IsClose(result.GetPixel(100, 50), SKColors.Red));
        }
    }
}
