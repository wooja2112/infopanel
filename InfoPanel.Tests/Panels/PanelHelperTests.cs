using InfoPanel.JonsboPanel;
using InfoPanel.Tests.TestSupport;
using InfoPanel.ThermalrightPanel;
using InfoPanel.TuringPanel;
using Xunit;

namespace InfoPanel.Tests.Panels
{
    public class PanelHelperTests
    {
        [Theory]
        [InlineData(512u, "512 KB")]
        [InlineData(1024u, "1.00 MB")]
        [InlineData(1536u, "1.50 MB")]
        [InlineData(3u * 1024 * 1024, "3.00 GB")]
        public void TuringStorage_FormatKB(uint kb, string expected)
        {
            using var _ = new CultureScope("en-US");

            Assert.Equal(expected, StorageInfo.FormatKB(kb));
        }

        [Fact]
        public void ScsiPoll_DetectsBootingSignature()
        {
            Assert.True(ScsiPanelDevice.IsDeviceBooting([0, 0, 0, 0, 0xA1, 0xA2, 0xA3, 0xA4]));
        }

        [Theory]
        [InlineData(new byte[] { 0, 0, 0, 0, 0xA1, 0xA2, 0xA3, 0x00 })]
        [InlineData(new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 })] // too short
        [InlineData(new byte[0])]
        public void ScsiPoll_NotBooting(byte[] response)
        {
            Assert.False(ScsiPanelDevice.IsDeviceBooting(response));
        }

        [Fact]
        public void Ms9132_UnknownResolution_HasNoVic()
        {
            Assert.Null(JonsboMs9132Device.GetVicForResolution(123, 456));
        }

        [Theory]
        [InlineData(0x24, 240, 240)]
        [InlineData(0x32, 320, 240)]
        [InlineData(0x64, 320, 320)]
        public void Thermalright_ScsiPollByteResolution(byte pollByte, int width, int height)
        {
            Assert.Equal((width, height), ThermalrightPanelModelDatabase.GetResolutionFromScsiPollByte(pollByte));
        }

        [Fact]
        public void Thermalright_UnknownScsiPollByte_IsNull()
        {
            Assert.Null(ThermalrightPanelModelDatabase.GetResolutionFromScsiPollByte(0x00));
        }
    }
}
