using InfoPanel.Services;
using SkiaSharp;
using Xunit;

namespace InfoPanel.Tests.Panels
{
    public class Rgb565ConversionTests
    {
        private static SKBitmap Pixels(params SKColor[] colors)
        {
            var bitmap = new SKBitmap(new SKImageInfo(colors.Length, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (int x = 0; x < colors.Length; x++)
            {
                bitmap.SetPixel(x, 0, colors[x]);
            }
            return bitmap;
        }

        [Theory]
        [InlineData(255, 0, 0, 0xF800)]
        [InlineData(0, 255, 0, 0x07E0)]
        [InlineData(0, 0, 255, 0x001F)]
        [InlineData(255, 255, 255, 0xFFFF)]
        [InlineData(0, 0, 0, 0x0000)]
        [InlineData(0x07, 0x03, 0x07, 0x0000)] // low bits are truncated, not rounded
        [InlineData(0x08, 0x04, 0x08, 0x0821)]
        public void ConvertsByTruncation_LittleEndian(byte r, byte g, byte b, int expected)
        {
            using var bitmap = Pixels(new SKColor(r, g, b));

            var result = ThermalrightPanelDeviceTask.ConvertRgba8888ToRgb565(bitmap, bigEndian: false);

            Assert.Equal(new[] { (byte)(expected & 0xFF), (byte)(expected >> 8) }, result);
        }

        [Fact]
        public void BigEndian_SwapsByteOrder()
        {
            using var bitmap = Pixels(new SKColor(255, 0, 0));

            var result = ThermalrightPanelDeviceTask.ConvertRgba8888ToRgb565(bitmap, bigEndian: true);

            Assert.Equal(new byte[] { 0xF8, 0x00 }, result);
        }

        [Fact]
        public void OutputIsTwoBytesPerPixel_InRowOrder()
        {
            using var bitmap = new SKBitmap(new SKImageInfo(3, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            bitmap.Erase(SKColors.Black);
            bitmap.SetPixel(2, 1, SKColors.White); // last pixel

            var result = ThermalrightPanelDeviceTask.ConvertRgba8888ToRgb565(bitmap, bigEndian: false);

            Assert.Equal(3 * 2 * 2, result.Length);
            Assert.Equal(0xFF, result[^1]);
            Assert.Equal(0xFF, result[^2]);
            Assert.All(result[..^2], b => Assert.Equal(0, b));
        }
    }
}
