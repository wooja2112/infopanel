using InfoPanel.Models;
using InfoPanel.Tests.TestSupport;
using SkiaSharp;
using System.IO;
using Xunit;

namespace InfoPanel.Tests.Models
{
    public class LockedImageTests
    {
        private static readonly ImageDisplayItem? NoItem = null;

        [Fact]
        public void LoadsRasterImageFromFile()
        {
            var path = TestBitmaps.WriteTempPng(4, 3, SKColors.Red);
            try
            {
                using var image = new LockedImage(path, NoItem);

                Assert.True(image.Loaded);
                Assert.Equal(LockedImage.ImageType.SK, image.Type);
                Assert.Equal(4, image.Width);
                Assert.Equal(3, image.Height);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AccessSK_ProvidesImageAtRequestedSize()
        {
            var path = TestBitmaps.WriteTempPng(4, 4, SKColors.Red);
            try
            {
                using var image = new LockedImage(path, NoItem);
                SKSizeI? size = null;

                image.AccessSK(8, 6, img => size = new SKSizeI(img.Width, img.Height), cache: false);

                Assert.Equal(new SKSizeI(8, 6), size);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AccessSK_AfterDispose_SkipsWithoutThrowing()
        {
            // Regression: a URL refresh evicts and disposes the cached image while render threads
            // may still hold it. Access must skip the frame instead of throwing.
            var path = TestBitmaps.WriteTempPng(4, 4, SKColors.Red);
            try
            {
                var image = new LockedImage(path, NoItem);
                image.Dispose();
                var called = false;

                image.AccessSK(4, 4, _ => called = true);
                image.AccessSVG(_ => called = true);

                Assert.False(called);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void MissingFile_IsNotLoaded_AndAccessIsANoOp()
        {
            using var image = new LockedImage(Path.Combine(Path.GetTempPath(), "does-not-exist-" + System.Guid.NewGuid() + ".png"), NoItem);
            var called = false;

            image.AccessSK(4, 4, _ => called = true);

            Assert.False(image.Loaded);
            Assert.False(called);
        }

        [Fact]
        public void LoadsSvg()
        {
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"10\">"
                + "<rect width=\"20\" height=\"10\" fill=\"red\"/>"
                + "<!--" + new string(' ', 600) + "-->"
                + "</svg>";
            var path = TestBitmaps.WriteTempFile(".svg", svg);
            try
            {
                using var image = new LockedImage(path, NoItem);

                Assert.True(image.Loaded);
                Assert.Equal(LockedImage.ImageType.SVG, image.Type);
                Assert.Equal(20, image.Width);
                Assert.Equal(10, image.Height);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LoadsSmallSvg()
        {
            // SVG detection only inspected files of 512 bytes or more, so small icons were
            // handed to the raster codec and failed to load.
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><circle cx=\"8\" cy=\"8\" r=\"8\"/></svg>";
            var path = TestBitmaps.WriteTempFile(".svg", svg);
            try
            {
                using var image = new LockedImage(path, NoItem);

                Assert.True(image.Loaded);
                Assert.Equal(LockedImage.ImageType.SVG, image.Type);
                Assert.Equal(16, image.Width);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
