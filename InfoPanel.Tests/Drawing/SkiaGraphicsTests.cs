using InfoPanel.Drawing;
using Xunit;

namespace InfoPanel.Tests.Drawing
{
    public class SkiaGraphicsTests
    {
        [Theory]
        [InlineData("Arial Bold", "Arial")]
        [InlineData("Roboto Condensed Light", "Roboto")]
        [InlineData("Futura BT Medium", "Futura")]
        [InlineData("Segoe UI", "Segoe UI")]
        [InlineData("Arial Bold Italic Pro", "Arial Pro")]
        public void ExtractBaseFamilyName_RemovesStyleDescriptors(string fontName, string expected)
        {
            Assert.Equal(expected, SkiaGraphics.ExtractBaseFamilyName(fontName));
        }

        [Theory]
        [InlineData("Subtle Sans Bold", "Subtle Sans")]       // "bt" inside "Subtle"
        [InlineData("Blackadder ITC", "Blackadder ITC")]      // "Black" prefix
        [InlineData("Lightning Script", "Lightning Script")]  // "Light" prefix
        [InlineData("Widescreen", "Widescreen")]              // "Wide" prefix
        public void ExtractBaseFamilyName_OnlyRemovesWholeWords(string fontName, string expected)
        {
            Assert.Equal(expected, SkiaGraphics.ExtractBaseFamilyName(fontName));
        }
    }
}
