// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class PersistentStorageValidationTest : IDisposable
    {
        struct DeviceIdentity
        {
            public string DeviceId { get; set; }

            public string IotHubHostname { get; set; }

            public string ModuleId { get; set; }

            [JsonProperty(Required = Required.AllowNull, PropertyName = "ModuleGenerationId")]
            [JsonConverter(typeof(OptionConverter<string>))]
            public Option<string> ModuleGenerationId { get; set; }

            public DeviceIdentity(string devId, string iothubHostname, string moduleId, Option<string> moduleGenId)
            {
                this.DeviceId = devId;
                this.IotHubHostname = iothubHostname;
                this.ModuleId = moduleId;
                this.ModuleGenerationId = moduleGenId;
            }
        }

        public void Dispose()
        {
            Directory.Delete("test", true);
        }

        [Fact]
        public void ValidateStorageIdentityTest()
        {
            DeviceIdentity savedIdentity = new DeviceIdentity("dev1", "hub1", "mod1", Option.Some("modgen1"));
            Directory.CreateDirectory("test");
            string filepath = Path.Combine("test", "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(savedIdentity);
            File.WriteAllText(filepath, json);

            ILogger<PersistentStorageValidationTest> mocklogger = Mock.Of<ILogger<PersistentStorageValidationTest>>();

            Assert.True(PersistentStorageValidation.ValidateStorageIdentity("test", "dev1", "hub1", "mod1", Option.Some("modgen1"), mocklogger));
        }

        [Fact]
        public void ValidateStorageIdentityTestDifferentIdentity()
        {
            DeviceIdentity savedIdentity = new DeviceIdentity("dev1", "hub1", "mod1", Option.Some("modgen2"));
            Directory.CreateDirectory("test");
            string filepath = Path.Combine("test", "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(savedIdentity);
            File.WriteAllText(filepath, json);

            ILogger<PersistentStorageValidationTest> mocklogger = Mock.Of<ILogger<PersistentStorageValidationTest>>();

            Assert.False(PersistentStorageValidation.ValidateStorageIdentity("test", "dev1", "hub1", "mod1", Option.Some("modgen3"), mocklogger));
        }
    }
}
