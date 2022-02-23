// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class PersistentStorageValidationTest
    {
        public class FilesIOHelper : PersistentStorageValidation.IFilesIOHelper
        {
            private string savedidentity;
            private bool identityExists;

            public FilesIOHelper(DeviceIdentity identity, bool identityExists)
            {
                this.savedidentity = JsonConvert.SerializeObject(identity);
                this.identityExists = identityExists;
            }

            public void DeleteAndCreateDirectory(string storagePath)
            {
                return;
            }

            public void WriteAllText(string filePath, string json)
            {
                this.savedidentity = json;
            }

            public string ReadAllText(string filePath)
            {
                return this.savedidentity;
            }

            public bool Exists(string filePath)
            {
                return this.identityExists;
            }
        }

        public struct DeviceIdentity
        {
            public string DeviceId { get; set; }

            public string IotHubHostname { get; set; }

            public string ModuleId { get; set; }

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

        [Fact]
        public void ValidateStorageIdentityTest()
        {
            DeviceIdentity savedIdentity = new DeviceIdentity("dev1", "hub1", "mod1", Option.Some("modgen1"));
            FilesIOHelper filehelper = new FilesIOHelper(savedIdentity, true);
            ILogger<PersistentStorageValidation> mocklogger = Mock.Of<ILogger<PersistentStorageValidation>>();
            PersistentStorageValidation validateStorage = new PersistentStorageValidation(filehelper);

            Assert.True(validateStorage.ValidateStorageIdentity("test", "dev1", "hub1", "mod1", Option.Some("modgen1"), mocklogger));
        }

        [Fact]
        public void ValidateStorageIdentityTestDifferentIdentity()
        {
            DeviceIdentity savedIdentity = new DeviceIdentity("dev2", "hub1", "mod1", Option.Some("modgen1"));
            FilesIOHelper filehelper = new FilesIOHelper(savedIdentity, true);
            ILogger<PersistentStorageValidation> mocklogger = Mock.Of<ILogger<PersistentStorageValidation>>();
            PersistentStorageValidation validateStorage = new PersistentStorageValidation(filehelper);

            Assert.False(validateStorage.ValidateStorageIdentity("test", "dev1", "hub1", "mod1", Option.Some("modgen1"), mocklogger));
        }
    }
}
