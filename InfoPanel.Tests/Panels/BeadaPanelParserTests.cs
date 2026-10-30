using InfoPanel.BeadaPanel;
using InfoPanel.BeadaPanel.StatusLink;
using System;
using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace InfoPanel.Tests.Panels
{
    public class BeadaPanelParserTests
    {
        private static byte[] BuildResponse(
            string protocol = "STATUS-LINK",
            byte messageType = (byte)StatusLinkMessageType.GetPanelInfo,
            byte modelByte = (byte)BeadaPanelModel.Model5,
            string serial = "ABC123")
        {
            var buffer = new byte[100];
            Encoding.ASCII.GetBytes(protocol).CopyTo(buffer, 0);
            buffer[12] = messageType;

            var payload = buffer.AsSpan(20);
            BinaryPrimitives.WriteUInt16LittleEndian(payload, 0x0102); // firmware
            payload[2] = 2;                                             // PanelLink version
            payload[3] = 1;                                             // StatusLink version
            payload[4] = 3;                                             // platform
            payload[5] = modelByte;
            Encoding.ASCII.GetBytes(serial).CopyTo(payload[6..]);
            BinaryPrimitives.WriteUInt16LittleEndian(payload[70..], 800);
            BinaryPrimitives.WriteUInt16LittleEndian(payload[72..], 480);
            BinaryPrimitives.WriteUInt32LittleEndian(payload[74..], 123456);
            payload[78] = 100;
            payload[79] = 75;
            return buffer;
        }

        [Fact]
        public void ParsesAllFields()
        {
            var info = BeadaPanelParser.ParsePanelInfoResponse(BuildResponse());

            Assert.NotNull(info);
            Assert.Equal(0x0102, info!.FirmwareVersion);
            Assert.Equal(2, info.PanelLinkVersion);
            Assert.Equal(1, info.StatusLinkVersion);
            Assert.Equal(3, info.Platform);
            Assert.Equal(BeadaPanelModel.Model5, info.Model);
            Assert.Equal("ABC123", info.SerialNumber);
            Assert.Equal(800, info.ResolutionX);
            Assert.Equal(480, info.ResolutionY);
            Assert.Equal(123456u, info.StorageSizeKB);
            Assert.Equal(100, info.MaxBrightness);
            Assert.Equal(75, info.CurrentBrightness);
            Assert.Same(BeadaPanelModelDatabase.Models[BeadaPanelModel.Model5], info.ModelInfo);
        }

        [Fact]
        public void NonAlphanumericSerial_IsCleared()
        {
            var info = BeadaPanelParser.ParsePanelInfoResponse(BuildResponse(serial: "AB-12"));

            Assert.NotNull(info);
            Assert.Equal(string.Empty, info!.SerialNumber);
        }

        [Fact]
        public void ShortBuffer_ReturnsNull()
        {
            Assert.Null(BeadaPanelParser.ParsePanelInfoResponse(new byte[99]));
        }

        [Fact]
        public void WrongProtocolHeader_ReturnsNull()
        {
            Assert.Null(BeadaPanelParser.ParsePanelInfoResponse(BuildResponse(protocol: "PANEL-LINK!")));
        }

        [Fact]
        public void WrongMessageType_ReturnsNull()
        {
            Assert.Null(BeadaPanelParser.ParsePanelInfoResponse(BuildResponse(messageType: (byte)StatusLinkMessageType.SetBacklight)));
        }

        [Fact]
        public void UnknownModel_ReturnsNull()
        {
            Assert.Null(BeadaPanelParser.ParsePanelInfoResponse(BuildResponse(modelByte: 200)));
        }
    }
}
