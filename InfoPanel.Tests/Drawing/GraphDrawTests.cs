using InfoPanel.Drawing;
using System;
using Xunit;

namespace InfoPanel.Tests.Drawing
{
    public class GraphDrawTests
    {
        [Theory]
        [InlineData(50, 0, 100, 0.5)]
        [InlineData(0, 0, 100, 0.0)]
        [InlineData(100, 0, 100, 1.0)]
        [InlineData(75, 50, 100, 0.5)]
        [InlineData(-10, 0, 100, 0.0)]
        public void NormalizeSample_MapsIntoUnitRange(double value, double min, double max, double expected)
        {
            Assert.Equal(expected, GraphDraw.NormalizeSample(value, min, max), precision: 9);
        }

        [Fact]
        public void NormalizeSample_ClampsAboveMax_WhenMinIsNonZero()
        {
            // Regression: the line graph clamped to maxValue instead of (maxValue - minValue),
            // so 150 in [50, 100] normalized to 2.0 and was drawn off the top of the chart.
            Assert.Equal(1.0, GraphDraw.NormalizeSample(150, 50, 100));
        }

        [Theory]
        [InlineData(50, 50)]  // empty range
        [InlineData(100, 50)] // inverted range
        public void NormalizeSample_ReturnsZero_ForDegenerateRange(double min, double max)
        {
            Assert.Equal(0.0, GraphDraw.NormalizeSample(75, min, max));
        }

        [Fact]
        public void NormalizeSample_ReturnsZero_ForNaN()
        {
            Assert.Equal(0.0, GraphDraw.NormalizeSample(double.NaN, 0, 100));
        }

        [Fact]
        public void NormalizeSample_ClampsInfinities()
        {
            Assert.Equal(1.0, GraphDraw.NormalizeSample(double.PositiveInfinity, 0, 100));
            Assert.Equal(0.0, GraphDraw.NormalizeSample(double.NegativeInfinity, 0, 100));
        }

        [Fact]
        public void ResolveAutoRange_ReturnsUserRange_WhenAutoValueOff()
        {
            var range = GraphDraw.ResolveAutoRange(Guid.NewGuid(), [5, 500], 0, 100, autoValue: false);

            Assert.Equal((0d, 100d), range);
        }

        [Fact]
        public void ResolveAutoRange_ReturnsUserRange_ForEmptySamples()
        {
            var range = GraphDraw.ResolveAutoRange(Guid.NewGuid(), [], 0, 100, autoValue: true);

            Assert.Equal((0d, 100d), range);
        }

        [Fact]
        public void ResolveAutoRange_SeedsFromSamples_OnFirstCall()
        {
            var range = GraphDraw.ResolveAutoRange(Guid.NewGuid(), [20, 80, 50], 0, 100, autoValue: true);

            Assert.Equal((20d, 80d), range);
        }

        [Fact]
        public void ResolveAutoRange_FallsBackToUserRange_ForFlatWindowOnFirstCall()
        {
            var range = GraphDraw.ResolveAutoRange(Guid.NewGuid(), [42, 42, 42], 0, 100, autoValue: true);

            Assert.Equal((0d, 100d), range);
        }

        [Fact]
        public void ResolveAutoRange_ExpandsImmediately_OnNewExtremes()
        {
            var id = Guid.NewGuid();
            GraphDraw.ResolveAutoRange(id, [20, 80], 0, 100, autoValue: true);

            var range = GraphDraw.ResolveAutoRange(id, [10, 90], 0, 100, autoValue: true);

            Assert.Equal((10d, 90d), range);
        }

        [Fact]
        public void ResolveAutoRange_ContractsGradually_WhenWindowNarrows()
        {
            var id = Guid.NewGuid();
            GraphDraw.ResolveAutoRange(id, [0, 100], 0, 100, autoValue: true);

            var (min, max) = GraphDraw.ResolveAutoRange(id, [40, 60], 0, 100, autoValue: true);

            // Moves 5% of the way towards the new extremes per call.
            Assert.Equal(2, min, precision: 9);
            Assert.Equal(98, max, precision: 9);
        }
    }
}
