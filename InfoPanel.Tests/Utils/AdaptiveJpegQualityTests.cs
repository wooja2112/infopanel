using InfoPanel.Utils;
using Xunit;

namespace InfoPanel.Tests.Utils
{
    public class AdaptiveJpegQualityTests
    {
        [Fact]
        public void StartQuality_WithNothingRemembered_UsesConfigured()
        {
            Assert.Equal(90, AdaptiveJpegQuality.GetStartQuality(null, 90));
        }

        [Fact]
        public void StartQuality_UsesRememberedQuality()
        {
            Assert.Equal(70, AdaptiveJpegQuality.GetStartQuality(70, 90));
        }

        [Fact]
        public void StartQuality_NeverExceedsConfigured_WhenUserLowersQuality()
        {
            Assert.Equal(80, AdaptiveJpegQuality.GetStartQuality(95, 80));
        }

        [Fact]
        public void NextQuality_StepsUp_WhenThereIsHeadroom()
        {
            Assert.Equal(75, AdaptiveJpegQuality.GetNextQuality(70, encodedSize: 10_000, maxBytes: 100_000, configuredQuality: 90));
        }

        [Fact]
        public void NextQuality_StepUpIsCappedAtConfigured()
        {
            Assert.Equal(90, AdaptiveJpegQuality.GetNextQuality(88, encodedSize: 10_000, maxBytes: 100_000, configuredQuality: 90));
        }

        [Theory]
        [InlineData(75_000)] // exactly at the headroom threshold
        [InlineData(99_000)]
        public void NextQuality_Holds_WithoutClearHeadroom(long encodedSize)
        {
            Assert.Equal(70, AdaptiveJpegQuality.GetNextQuality(70, encodedSize, maxBytes: 100_000, configuredQuality: 90));
        }
    }
}
