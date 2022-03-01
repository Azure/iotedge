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
        public void Dispose()
        {
            Directory.Delete("test", true);
        }

        [Fact]
        public void ValidateStorageIdentityTest()
        {
            PersistentStorageValidation.DeviceIdentity savedIdentity = new PersistentStorageValidation.DeviceIdentity("dev1", "hub1", "mod1", "modgen1");
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
            PersistentStorageValidation.DeviceIdentity savedIdentity = new PersistentStorageValidation.DeviceIdentity("dev1", "hub1", "mod1", "modgen2");
            Directory.CreateDirectory("test");
            string filepath = Path.Combine("test", "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(savedIdentity);
            File.WriteAllText(filepath, json);

            ILogger<PersistentStorageValidationTest> mocklogger = Mock.Of<ILogger<PersistentStorageValidationTest>>();

            Assert.False(PersistentStorageValidation.ValidateStorageIdentity("test", "dev1", "hub1", "mod1", Option.Some("modgen3"), mocklogger));
        }

        [Fact]
        public void ValidateStorageIdentityTestNoModuleGenId()
        {
            PersistentStorageValidation.DeviceIdentity savedIdentity = new PersistentStorageValidation.DeviceIdentity("dev1", "hub1", "mod1", null);
            Directory.CreateDirectory("test");
            string filepath = Path.Combine("test", "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(savedIdentity);
            File.WriteAllText(filepath, json);

            ILogger<PersistentStorageValidationTest> mocklogger = Mock.Of<ILogger<PersistentStorageValidationTest>>();

            Assert.True(PersistentStorageValidation.ValidateStorageIdentity("test", "dev1", "hub1", "mod1", Option.None<string>(), mocklogger));
        }
    }
}
