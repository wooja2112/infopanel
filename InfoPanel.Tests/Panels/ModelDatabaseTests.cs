using InfoPanel.BeadaPanel;
using InfoPanel.JlPanel;
using InfoPanel.JonsboPanel;
using InfoPanel.LianLiPanel;
using InfoPanel.ThermalrightPanel;
using InfoPanel.ThermaltakePanel;
using InfoPanel.TuringPanel;
using InfoPanel.VmaxPanel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace InfoPanel.Tests.Panels
{
    /// <summary>
    /// Data-integrity checks for the per-vendor model databases. These catch the copy/paste
    /// mistakes that are easy to make when adding a new SKU: a dictionary key that doesn't match
    /// the entry, a VID/PID that the scanner never looks for, or two models sharing a VID/PID so
    /// the second one can never be selected.
    /// </summary>
    public class ModelDatabaseTests
    {
        public record ModelRow(string Vendor, string Key, string Model, string Name, int Width, int Height, int Vid, int Pid)
        {
            public override string ToString() => $"{Vendor}/{Key}";
        }

        private static IEnumerable<ModelRow> AllModels()
        {
            foreach (var (k, m) in BeadaPanelModelDatabase.Models)
                yield return new("Beada", k.ToString(), k.ToString(), m.Name, m.Width, m.Height, 0, 0);
            foreach (var (k, m) in TuringPanelModelDatabase.Models)
                yield return new("Turing", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
            foreach (var (k, m) in LianLiPanelModelDatabase.Models)
                yield return new("LianLi", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
            foreach (var (k, m) in ThermalrightPanelModelDatabase.Models)
                yield return new("Thermalright", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
            foreach (var (k, m) in ThermaltakePanelModelDatabase.Models)
                yield return new("Thermaltake", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
            foreach (var (k, m) in JlPanelModelDatabase.Models)
                yield return new("Jl", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
            foreach (var (k, m) in JonsboPanelModelDatabase.Models)
                yield return new("Jonsbo", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
            foreach (var (k, m) in VmaxPanelModelDatabase.Models)
                yield return new("Vmax", k.ToString(), m.Model.ToString(), m.Name, m.Width, m.Height, m.VendorId, m.ProductId);
        }

        public static TheoryData<ModelRow> Models()
        {
            var data = new TheoryData<ModelRow>();
            foreach (var row in AllModels()) data.Add(row);
            return data;
        }

        [Theory]
        [MemberData(nameof(Models))]
        public void Model_IsWellFormed(ModelRow row)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Name), "Name is empty");
            Assert.True(row.Width > 0, "Width must be positive");
            Assert.True(row.Height > 0, "Height must be positive");
            Assert.Equal(row.Key, row.Model);
        }

        public static TheoryData<string, (int Vid, int Pid)[], (int Vid, int Pid)[]> ScannedVendors() => new()
        {
            { "Jl", JlPanelModelDatabase.SupportedDevices, JlPanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "Jonsbo", JonsboPanelModelDatabase.SupportedDevices, JonsboPanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "Vmax", VmaxPanelModelDatabase.SupportedDevices, VmaxPanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "Thermaltake", ThermaltakePanelModelDatabase.SupportedDevices, ThermaltakePanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            // Runtime-variant Thermalright models are identified after init and carry no VID/PID.
            { "Thermalright", ThermalrightPanelModelDatabase.SupportedDevices, ThermalrightPanelModelDatabase.Models.Values.Where(m => m.VendorId != 0).Select(m => (m.VendorId, m.ProductId)).ToArray() },
        };

        [Theory]
        [MemberData(nameof(ScannedVendors))]
        public void EveryModelVidPid_IsScannedFor(string vendor, (int Vid, int Pid)[] scanned, (int Vid, int Pid)[] modelIds)
        {
            var missing = modelIds.Distinct().Where(id => !scanned.Contains(id)).ToList();

            Assert.True(missing.Count == 0, $"{vendor}: models never discovered because their VID/PID is not scanned: " +
                string.Join(", ", missing.Select(id => $"{id.Vid:X4}:{id.Pid:X4}")));
        }

        /// <summary>
        /// Thermalright is excluded: its HID panels (e.g. 0418:5303/5304) are scanned by VID/PID
        /// but identified at runtime from the PM byte, so no model entry carries those IDs.
        /// </summary>
        public static TheoryData<string, (int Vid, int Pid)[], (int Vid, int Pid)[]> ScannedVendorsWithStaticIds()
        {
            var data = new TheoryData<string, (int Vid, int Pid)[], (int Vid, int Pid)[]>();
            foreach (var row in ScannedVendors())
            {
                if ((string)row[0] != "Thermalright")
                    data.Add((string)row[0], ((int, int)[])row[1], ((int, int)[])row[2]);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(ScannedVendorsWithStaticIds))]
        public void EveryScannedVidPid_BelongsToAModel(string vendor, (int Vid, int Pid)[] scanned, (int Vid, int Pid)[] modelIds)
        {
            var orphans = scanned.Where(id => !modelIds.Contains(id)).ToList();

            Assert.True(orphans.Count == 0, $"{vendor}: scanned VID/PID with no model: " +
                string.Join(", ", orphans.Select(id => $"{id.Vid:X4}:{id.Pid:X4}")));
        }

        public static TheoryData<string, (int Vid, int Pid)[]> FirstMatchLookupVendors() => new()
        {
            // Known gap: StripDisplay reuses the Chill Arc 360 PID ("matched by resolution"), but nothing
            // performs that match yet, so discovery always picks ChillArc360. See Jl_StripDisplay_* below.
            { "Jl", JlPanelModelDatabase.Models.Values.Where(m => m.Model != JlPanelModel.StripDisplay).Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "Jonsbo", JonsboPanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "Vmax", VmaxPanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "Thermaltake", ThermaltakePanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
            { "LianLi", LianLiPanelModelDatabase.Models.Values.Select(m => (m.VendorId, m.ProductId)).ToArray() },
        };

        /// <summary>These vendors resolve models with FirstOrDefault on VID/PID; a duplicate makes a model unreachable.</summary>
        [Theory]
        [MemberData(nameof(FirstMatchLookupVendors))]
        public void VidPid_IsUniquePerModel(string vendor, (int Vid, int Pid)[] ids)
        {
            var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            Assert.True(duplicates.Count == 0, $"{vendor}: VID/PID shared by several models: " +
                string.Join(", ", duplicates.Select(id => $"{id.Vid:X4}:{id.Pid:X4}")));
        }

        [Fact]
        public void LookupByVidPid_ReturnsTheModel()
        {
            foreach (var m in JlPanelModelDatabase.Models.Values.Where(m => m.Model != JlPanelModel.StripDisplay))
                Assert.Same(m, JlPanelModelDatabase.GetModelByVidPid(m.VendorId, m.ProductId));
            foreach (var m in JonsboPanelModelDatabase.Models.Values)
                Assert.Same(m, JonsboPanelModelDatabase.GetModelByVidPid(m.VendorId, m.ProductId));
            foreach (var m in VmaxPanelModelDatabase.Models.Values)
                Assert.Same(m, VmaxPanelModelDatabase.GetModelByVidPid(m.VendorId, m.ProductId));
            foreach (var m in ThermaltakePanelModelDatabase.Models.Values)
                Assert.Same(m, ThermaltakePanelModelDatabase.GetModelByVidPid(m.VendorId, m.ProductId));
            foreach (var m in LianLiPanelModelDatabase.Models.Values)
                Assert.Same(m, LianLiPanelModelDatabase.GetModelByVidPid(m.VendorId, m.ProductId));
        }

        [Fact]
        public void LookupByVidPid_UnknownDevice_ReturnsNull()
        {
            Assert.Null(JlPanelModelDatabase.GetModelByVidPid(0xFFFF, 0xFFFF));
            Assert.Null(JonsboPanelModelDatabase.GetModelByVidPid(0xFFFF, 0xFFFF));
            Assert.Null(VmaxPanelModelDatabase.GetModelByVidPid(0xFFFF, 0xFFFF));
            Assert.Null(ThermaltakePanelModelDatabase.GetModelByVidPid(0xFFFF, 0xFFFF));
            Assert.Null(LianLiPanelModelDatabase.GetModelByVidPid(0xFFFF, 0xFFFF));
            Assert.Null(ThermalrightPanelModelDatabase.GetModelByVidPid(0xFFFF, 0xFFFF));
        }

        /// <summary>
        /// Only USB models are resolved by VID/PID. Serial models share the generic CH340 bridge IDs
        /// (e.g. Turing 3.5" and XuanFang 3.5") and are told apart by the serial handshake.
        /// </summary>
        [Fact]
        public void Turing_UsbLookupRoundTrips_AndIsUnique()
        {
            var usbModels = TuringPanelModelDatabase.Models.Values.Where(m => m.IsUsbDevice).ToList();
            var keys = usbModels.Select(m => (m.VendorId, m.ProductId)).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());

            foreach (var m in usbModels)
            {
                Assert.True(TuringPanelModelDatabase.TryGetModelInfo(m.VendorId, m.ProductId, m.IsUsbDevice, out var found));
                Assert.Same(m, found);
            }
        }

        [Fact]
        public void LianLi_ResolvesModelFromWindowsDeviceId()
        {
            foreach (var m in LianLiPanelModelDatabase.Models.Values)
            {
                var deviceId = $@"USB\VID_{m.VendorId:X4}&PID_{m.ProductId:X4}\6&1A2B3C4D&0&1";

                Assert.Same(m, LianLiPanelModelDatabase.GetModelByDeviceId(deviceId));
                Assert.True(LianLiPanelModelDatabase.IsLianLiDeviceId(deviceId));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a device id")]
        [InlineData(@"USB\VID_FFFF&PID_FFFF\1")]
        public void LianLi_UnknownDeviceId_ReturnsNull(string? deviceId)
        {
            Assert.Null(LianLiPanelModelDatabase.GetModelByDeviceId(deviceId));
            Assert.False(LianLiPanelModelDatabase.IsLianLiDeviceId(deviceId));
        }

        /// <summary>Known gap: Model7 is a valid model byte with no database entry (specs not yet known).</summary>
        private static readonly BeadaPanelModel[] KnownMissingBeadaModels = [BeadaPanelModel.Model7];

        [Fact]
        public void Beada_EveryModelEnumValue_HasAnEntry()
        {
            var missing = Enum.GetValues<BeadaPanelModel>()
                .Where(m => !BeadaPanelModelDatabase.Models.ContainsKey(m))
                .Except(KnownMissingBeadaModels)
                .ToList();

            Assert.True(missing.Count == 0, "Panels reporting these models are rejected by the parser: " + string.Join(", ", missing));
        }

        [Fact(Skip = "Known gap: Model7 has no database entry, so ParsePanelInfoResponse rejects these panels.")]
        public void Beada_Model7_HasAnEntry()
        {
            Assert.True(BeadaPanelModelDatabase.Models.ContainsKey(BeadaPanelModel.Model7));
        }

        [Fact(Skip = "Known gap: StripDisplay shares the Chill Arc 360 VID/PID and nothing selects it by resolution, so it is never discovered.")]
        public void Jl_StripDisplay_IsDiscoverable()
        {
            var strip = JlPanelModelDatabase.Models[JlPanelModel.StripDisplay];

            Assert.Same(strip, JlPanelModelDatabase.GetModelByVidPid(strip.VendorId, strip.ProductId));
        }

        [Fact]
        public void Jonsbo_Ms9132Models_UseTheVicForTheirResolution()
        {
            foreach (var m in JonsboPanelModelDatabase.Models.Values.Where(m => m.TransportType == JonsboTransportType.Ms9132))
            {
                Assert.Equal(m.Vic, JonsboMs9132Device.GetVicForResolution(m.Width, m.Height));
            }
        }

        [Fact]
        public void Thermalright_ChiZhuPmTable_OnlyReferencesExistingModels()
        {
            // GetModelByChiZhuPM indexes Models[...] directly; a missing entry would throw at runtime.
            for (int pm = 0; pm <= byte.MaxValue; pm++)
            {
                for (int sub = 0; sub <= byte.MaxValue; sub++)
                {
                    ThermalrightPanelModelDatabase.GetModelByChiZhuPM((byte)pm, (byte)sub);
                }
            }
        }

        [Fact]
        public void Thermalright_PmAndSubBytes_AreUnambiguous()
        {
            var hidModels = ThermalrightPanelModelDatabase.Models.Values.Where(m => m.PmByte.HasValue).ToList();

            var exact = hidModels.Where(m => m.SubByte.HasValue)
                .GroupBy(m => (m.PmByte, m.SubByte, m.DeviceIdentifier)).Where(g => g.Count() > 1)
                .Select(g => string.Join("/", g.Select(m => m.Model))).ToList();
            var wildcard = hidModels.Where(m => !m.SubByte.HasValue)
                .GroupBy(m => (m.PmByte, m.DeviceIdentifier)).Where(g => g.Count() > 1)
                .Select(g => string.Join("/", g.Select(m => m.Model))).ToList();

            Assert.True(exact.Count == 0, "Models sharing PM+SUB: " + string.Join(", ", exact));
            Assert.True(wildcard.Count == 0, "Models sharing a wildcard PM: " + string.Join(", ", wildcard));
        }

        [Fact]
        public void Thermalright_GetModelByPM_FindsEveryHidModel()
        {
            foreach (var m in ThermalrightPanelModelDatabase.Models.Values.Where(m => m.PmByte.HasValue && m.TransportType == ThermalrightTransportType.Hid))
            {
                var found = ThermalrightPanelModelDatabase.GetModelByPM(m.PmByte!.Value, m.SubByte);

                Assert.NotNull(found);
                Assert.Equal(m.PmByte, found!.PmByte);
            }
        }
    }
}
