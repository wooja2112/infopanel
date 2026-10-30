using InfoPanel.Models;
using InfoPanel.Tests.TestSupport;
using Xunit;

namespace InfoPanel.Tests.Models
{
    public class SensorDisplayItemTests
    {
        private static SensorReading Reading(double now, string unit = "", double min = 0, double max = 0, double avg = 0)
            => new(min, max, avg, now, unit);

        [Fact]
        public void DefaultFormatting_RoundsToWholeNumberAndAppendsUnit()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem();

            Assert.Equal("42°C", item.EvaluateText(Reading(42.4, "°C")));
        }

        [Theory]
        [InlineData("GB", 12.345, "12.3GB")]
        [InlineData("MB/s", 12.345, "12.35MB/s")]
        [InlineData("mbar", 12.345, "12.35mbar")]
        [InlineData("V", 1.23456, "1.235V")]
        [InlineData("RPM", 1499.6, "1500RPM")]
        public void UnitDeterminesDefaultPrecision(string unit, double value, string expected)
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem();

            Assert.Equal(expected, item.EvaluateText(Reading(value, unit)));
        }

        [Theory]
        [InlineData(1, "3.1")]
        [InlineData(2, "3.14")]
        [InlineData(3, "3.142")]
        public void OverridePrecision_UsesConfiguredDecimals(int precision, string expected)
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { OverridePrecision = true, Precision = precision, ShowUnit = false };

            Assert.Equal(expected, item.EvaluateText(Reading(3.14159)));
        }

        [Fact]
        public void OverridePrecisionZero_Floors()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { OverridePrecision = true, Precision = 0, ShowUnit = false };

            Assert.Equal("3", item.EvaluateText(Reading(3.99)));
        }

        [Fact]
        public void MultiplicationAndAddition_AreApplied()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { MultiplicationModifier = 2, AdditionModifier = 1, ShowUnit = false };

            Assert.Equal("21", item.EvaluateText(Reading(10)));
        }

        [Fact]
        public void DivisionToggle_DividesByModifier()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem
            {
                DivisionToggle = true,
                MultiplicationModifier = 4,
                OverridePrecision = true,
                Precision = 1,
                ShowUnit = false,
            };

            Assert.Equal("2.5", item.EvaluateText(Reading(10)));
        }

        [Fact]
        public void DivisionToggle_WithZeroModifier_DoesNotDivide()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { DivisionToggle = true, MultiplicationModifier = 0, AdditionModifier = 5, ShowUnit = false };

            Assert.Equal("15", item.EvaluateText(Reading(10)));
        }

        [Fact]
        public void AbsoluteAddition_IsOnByDefault()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { ShowUnit = false };

            Assert.Equal("5", item.EvaluateText(Reading(-5)));
        }

        [Fact]
        public void AbsoluteAdditionOff_KeepsSign()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { AbsoluteAddition = false, ShowUnit = false };

            Assert.Equal("-5", item.EvaluateText(Reading(-5)));
        }

        [Theory]
        [InlineData(SensorValueType.NOW, "4")]
        [InlineData(SensorValueType.MIN, "1")]
        [InlineData(SensorValueType.MAX, "9")]
        [InlineData(SensorValueType.AVERAGE, "5")]
        public void ValueType_SelectsReadingField(SensorValueType valueType, string expected)
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { ValueType = valueType, ShowUnit = false };

            Assert.Equal(expected, item.EvaluateText(Reading(now: 4, min: 1, max: 9, avg: 5)));
        }

        [Fact]
        public void ThousandsSeparator_EnUs()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { ShowThousandsSeparator = true };

            Assert.Equal("1,234,567MB", item.EvaluateText(Reading(1234567, "MB")));
            Assert.Equal("1,234.5GB", item.EvaluateText(Reading(1234.5, "GB")));
        }

        [Fact]
        public void ThousandsSeparator_UsesCurrentCulture()
        {
            // Regression: the formatted value was re-parsed with the invariant culture, so the
            // de-DE decimal comma was read as a thousands separator ("1234,5" -> "12,345").
            using var _ = new CultureScope("de-DE");
            var item = new SensorDisplayItem { ShowThousandsSeparator = true };

            Assert.Equal("1.234,5GB", item.EvaluateText(Reading(1234.5, "GB")));
        }

        [Fact]
        public void WithoutThousandsSeparator_UsesCurrentCultureDecimalSeparator()
        {
            using var _ = new CultureScope("de-DE");
            var item = new SensorDisplayItem();

            Assert.Equal("1234,5GB", item.EvaluateText(Reading(1234.5, "GB")));
        }

        [Fact]
        public void TextValue_IsShownAsIs()
        {
            var item = new SensorDisplayItem();

            Assert.Equal("Running", item.EvaluateText(new SensorReading("Running")));
        }

        [Fact]
        public void TextValue_ThousandsSeparator_ParsesInvariantText()
        {
            using var _ = new CultureScope("de-DE");
            var item = new SensorDisplayItem { ShowThousandsSeparator = true };

            Assert.Equal("1,234.5", item.EvaluateText(new SensorReading("1234.5")));
        }

        [Fact]
        public void OverrideUnit_ReplacesSensorUnit()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { OverrideUnit = true, Unit = " deg" };

            Assert.Equal("42 deg", item.EvaluateText(Reading(42, "°C")));
        }

        [Fact]
        public void ShowName_PrefixesName()
        {
            using var _ = new CultureScope("en-US");
            var item = new SensorDisplayItem { Name = "CPU", ShowName = true };

            Assert.Equal("CPU 42°C", item.EvaluateText(Reading(42, "°C")));
        }
    }
}
