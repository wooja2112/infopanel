using InfoPanel.Extensions;
using Xunit;

namespace InfoPanel.Tests.Extensions
{
    public class StringExtensionsTests
    {
        [Theory]
        [InlineData("http://example.com/image.png", true)]
        [InlineData("https://example.com", true)]
        [InlineData("rtsp://camera.local/stream", false)]
        [InlineData("ftp://example.com/file", false)]
        [InlineData(@"C:\images\file.png", false)]
        [InlineData("not a url", false)]
        [InlineData("", false)]
        public void IsUrl_AcceptsOnlyHttpAndHttps(string value, bool expected)
        {
            Assert.Equal(expected, value.IsUrl());
        }

        [Theory]
        [InlineData("ABC123", true)]
        [InlineData("abc", true)]
        [InlineData("ABC-123", false)]
        [InlineData("ABC 123", false)]
        [InlineData("", false)]
        public void IsAlphanumeric(string value, bool expected)
        {
            Assert.Equal(expected, value.IsAlphanumeric());
            Assert.Equal(!expected, value.IsNotAlphanumeric());
        }
    }
}
