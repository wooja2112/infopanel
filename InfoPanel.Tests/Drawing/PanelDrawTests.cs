using InfoPanel.Drawing;
using SkiaSharp;
using Xunit;

namespace InfoPanel.Tests.Drawing
{
    public class PanelDrawTests
    {
        [Fact]
        public void RectToPath_ReturnsRect_WhenFullyInsideCanvas()
        {
            using var path = PanelDraw.RectToPath(new SKRect(10, 10, 50, 40), 0, 100, 100);

            Assert.NotNull(path);
            Assert.Equal(new SKRect(10, 10, 50, 40), path!.Bounds);
        }

        [Fact]
        public void RectToPath_ReturnsNull_WhenEntirelyOutsideCanvas()
        {
            using var path = PanelDraw.RectToPath(new SKRect(200, 200, 250, 250), 0, 100, 100);

            Assert.Null(path);
        }

        [Fact]
        public void RectToPath_ReturnsNull_WhenRotatedRectIsOutsideCanvas()
        {
            using var path = PanelDraw.RectToPath(new SKRect(200, 200, 250, 250), 45, 100, 100);

            Assert.Null(path);
        }

        [Fact]
        public void RectToPath_ClipsToCanvasInsetByHalfPen_WhenPartiallyOutside()
        {
            using var path = PanelDraw.RectToPath(new SKRect(-10, -10, 50, 50), 0, 100, 100, penWidth: 2);

            Assert.NotNull(path);
            var bounds = path!.Bounds;
            Assert.Equal(1, bounds.Left, precision: 3);
            Assert.Equal(1, bounds.Top, precision: 3);
            Assert.Equal(50, bounds.Right, precision: 3);
            Assert.Equal(50, bounds.Bottom, precision: 3);
        }

        [Fact]
        public void RectToPath_RotatesAroundRectCenter()
        {
            // A 40x20 rect rotated 90° around its center becomes 20x40 with the same center.
            using var path = PanelDraw.RectToPath(new SKRect(30, 40, 70, 60), 90, 100, 100);

            Assert.NotNull(path);
            var bounds = path!.Bounds;
            Assert.Equal(20, bounds.Width, precision: 3);
            Assert.Equal(40, bounds.Height, precision: 3);
            Assert.Equal(50, bounds.MidX, precision: 3);
            Assert.Equal(50, bounds.MidY, precision: 3);
        }
    }
}
