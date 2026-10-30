using InfoPanel.JlPanel;
using System;
using System.Linq;
using Xunit;

namespace InfoPanel.Tests.Panels
{
    /// <summary>Frame layout per JL-PROTOCOL.md: 0x55 0xAA [len LE16] [cmd] [payload] [checksum LE16].</summary>
    public class JlProtocolTests
    {
        [Fact]
        public void BuildFrame_WithoutPayload()
        {
            var frame = JlSerialDevice.BuildFrame(0x11, ReadOnlySpan<byte>.Empty);

            // sum = 0x55 + 0xAA + 0x07 + 0x00 + 0x11 = 0x117
            Assert.Equal(new byte[] { 0x55, 0xAA, 0x07, 0x00, 0x11, 0x17, 0x01 }, frame);
        }

        [Fact]
        public void BuildFrame_LengthCoversWholeFrame_LittleEndian()
        {
            var payload = new byte[300];

            var frame = JlSerialDevice.BuildFrame(0x02, payload);

            Assert.Equal(307, frame.Length);
            Assert.Equal(307, frame[2] | (frame[3] << 8));
            Assert.Equal(0x02, frame[4]);
        }

        [Fact]
        public void BuildFrame_CopiesPayloadAfterCommand()
        {
            byte[] payload = [1, 2, 3, 4];

            var frame = JlSerialDevice.BuildFrame(0x03, payload);

            Assert.Equal(payload, frame[5..9]);
        }

        [Fact]
        public void BuildFrame_ChecksumIsSumOfPrecedingBytesModulo65536()
        {
            // 300 * 0xFF overflows 16 bits; the checksum must wrap.
            var payload = Enumerable.Repeat((byte)0xFF, 300).ToArray();

            var frame = JlSerialDevice.BuildFrame(0x7F, payload);

            int expected = frame.Take(frame.Length - 2).Sum(b => b) & 0xFFFF;
            Assert.Equal(expected, frame[^2] | (frame[^1] << 8));
        }
    }
}
