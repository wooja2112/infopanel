using InfoPanel.ViewModels.Components;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace InfoPanel.Tests.ViewModels
{
    public class SensorTreeFilterTests
    {
        private readonly TreeItem _cpu;
        private readonly TreeItem _cpuTemps;
        private readonly TreeItem _cpuPackage;
        private readonly TreeItem _cpuCore0;
        private readonly TreeItem _cpuClocks;
        private readonly TreeItem _cpuCore0Clock;
        private readonly TreeItem _gpu;
        private readonly TreeItem _gpuTemps;
        private readonly TreeItem _gpuHotSpot;
        private readonly List<TreeItem> _roots;

        public SensorTreeFilterTests()
        {
            // CPU [#0]: AMD Ryzen 7 9700X
            //   Temperature: CPU Package, Core 0
            //   Frequency:   Core 0 Clock
            // GPU [#0]: NVIDIA GeForce RTX 4080
            //   Temperature: GPU Hot Spot
            _cpuPackage = new TreeItem(1, "CPU Package");
            _cpuCore0 = new TreeItem(2, "Core 0");
            _cpuTemps = Node("Temperature", _cpuPackage, _cpuCore0);
            _cpuCore0Clock = new TreeItem(3, "Core 0 Clock");
            _cpuClocks = Node("Frequency", _cpuCore0Clock);
            _cpu = Node("CPU [#0]: AMD Ryzen 7 9700X", _cpuTemps, _cpuClocks);

            _gpuHotSpot = new TreeItem(4, "GPU Hot Spot");
            _gpuTemps = Node("Temperature", _gpuHotSpot);
            _gpu = Node("GPU [#0]: NVIDIA GeForce RTX 4080", _gpuTemps);

            _roots = [_cpu, _gpu];
        }

        private static TreeItem Node(string name, params TreeItem[] children)
        {
            var item = new TreeItem(name, name);
            foreach (var child in children) item.Children.Add(child);
            return item;
        }

        private IEnumerable<TreeItem> All()
        {
            IEnumerable<TreeItem> Walk(TreeItem i) => new[] { i }.Concat(i.Children.SelectMany(Walk));
            return _roots.SelectMany(Walk);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyFilter_ShowsEverything(string? filter)
        {
            var anyVisible = SensorTreeFilter.Apply(_roots, filter, expandMatches: true);

            Assert.True(anyVisible);
            Assert.All(All(), i => Assert.True(i.IsVisible));
        }

        [Fact]
        public void MatchingLeaf_IsShownWithItsAncestors()
        {
            SensorTreeFilter.Apply(_roots, "hot spot", expandMatches: true);

            Assert.True(_gpuHotSpot.IsVisible);
            Assert.True(_gpuTemps.IsVisible);
            Assert.True(_gpu.IsVisible);
            Assert.False(_cpu.IsVisible);
        }

        [Fact]
        public void MatchIsCaseInsensitive()
        {
            SensorTreeFilter.Apply(_roots, "HOT SPOT", expandMatches: true);

            Assert.True(_gpuHotSpot.IsVisible);
        }

        [Fact]
        public void MatchingParent_ShowsAllItsChildren()
        {
            SensorTreeFilter.Apply(_roots, "ryzen", expandMatches: true);

            Assert.True(_cpu.IsVisible);
            Assert.True(_cpuPackage.IsVisible);
            Assert.True(_cpuCore0Clock.IsVisible);
            Assert.False(_gpu.IsVisible);
        }

        [Fact]
        public void EveryWord_MustMatchSomewhereInThePath()
        {
            // "cpu" matches the hardware name, "temp" the group: only CPU temperatures remain.
            SensorTreeFilter.Apply(_roots, "cpu temp", expandMatches: true);

            Assert.True(_cpuPackage.IsVisible);
            Assert.True(_cpuCore0.IsVisible);
            Assert.False(_cpuClocks.IsVisible);
            Assert.False(_cpuCore0Clock.IsVisible);
            Assert.False(_gpu.IsVisible);
        }

        [Fact]
        public void NoMatches_ReturnsFalse()
        {
            var anyVisible = SensorTreeFilter.Apply(_roots, "fan", expandMatches: true);

            Assert.False(anyVisible);
            Assert.All(All(), i => Assert.False(i.IsVisible));
        }

        [Fact]
        public void ExpandsNodesThatLeadToMatches()
        {
            SensorTreeFilter.Apply(_roots, "core 0 clock", expandMatches: true);

            Assert.True(_cpu.IsExpanded);
            Assert.True(_cpuClocks.IsExpanded);
            Assert.False(_cpuTemps.IsExpanded);
            Assert.False(_gpu.IsExpanded);
        }

        [Fact]
        public void ClearingFilter_RestoresPreviousExpansion()
        {
            _gpu.IsExpanded = true; // the user had the GPU open, CPU closed

            SensorTreeFilter.Apply(_roots, "c", expandMatches: true);
            SensorTreeFilter.Apply(_roots, "cp", expandMatches: true);
            SensorTreeFilter.Apply(_roots, "cpu", expandMatches: true);
            SensorTreeFilter.Apply(_roots, "", expandMatches: true);

            Assert.True(_gpu.IsExpanded);
            Assert.False(_cpu.IsExpanded);
            Assert.False(_cpuTemps.IsExpanded);
            Assert.All(All(), i => Assert.True(i.IsVisible));
        }

        [Fact]
        public void ReapplyingAfterTreeUpdate_LeavesExpansionAlone()
        {
            SensorTreeFilter.Apply(_roots, "package", expandMatches: true);
            _cpu.IsExpanded = false; // user collapses a result

            var newSensor = new TreeItem(5, "Core 1");
            _cpuTemps.Children.Add(newSensor);
            SensorTreeFilter.Apply(_roots, "package", expandMatches: false);

            Assert.False(newSensor.IsVisible);
            Assert.False(_cpu.IsExpanded);
        }

        [Fact]
        public void ParseTerms_SplitsOnAnyWhitespace()
        {
            Assert.Equal(["cpu", "temp"], SensorTreeFilter.ParseTerms("  cpu \t temp "));
            Assert.Empty(SensorTreeFilter.ParseTerms(null));
        }
    }
}
