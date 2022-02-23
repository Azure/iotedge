// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class PersistentStorageValidation
    {
        readonly IFilesIOHelper filesHelper;
        public PersistentStorageValidation(IFilesIOHelper filesHelper)
        {
            this.filesHelper = filesHelper;
        }

        public PersistentStorageValidation()
        {
            this.filesHelper = new FilesIOHelper();
        }

        public bool ValidateStorageIdentity(string storagePath, string deviceId, string iotHubHostname, string moduleId, Option<string> moduleGenerationId, ILogger logger)
        {
            DeviceIdentity currentIdentity = new DeviceIdentity(deviceId, iotHubHostname, moduleId, moduleGenerationId);
            string filepath = Path.Combine(storagePath, "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(currentIdentity);

            if (!this.filesHelper.Exists(filepath))
            {
                this.filesHelper.WriteAllText(filepath, json);
                return true;
            }
            else
            {
                DeviceIdentity savedIdentity = JsonConvert.DeserializeObject<DeviceIdentity>(this.filesHelper.ReadAllText(filepath));

                if (!currentIdentity.Equals(savedIdentity))
                {
                    logger.LogInformation("Persistent storage is for a different device identity {actual} than the current identity {current}. Deleting local storage.",  savedIdentity.DeviceId, currentIdentity.DeviceId);
                    this.filesHelper.DeleteAndCreateDirectory(storagePath);
                    this.filesHelper.WriteAllText(filepath, json);
                    return false;
                }

                return true;
            }
        }

        struct DeviceIdentity
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

        public interface IFilesIOHelper
        {
            public void DeleteAndCreateDirectory(string storagePath);

            public void WriteAllText(string filePath, string json);

            public string ReadAllText(string filePath);

            public bool Exists(string filePath);
        }

        public class FilesIOHelper : IFilesIOHelper
        {
            public void DeleteAndCreateDirectory(string storagePath)
            {
                Directory.Delete(storagePath, true);
                Directory.CreateDirectory(storagePath);
            }

            public void WriteAllText(string filePath, string json)
            {
                File.WriteAllText(filePath, json);
            }

            public string ReadAllText(string filePath)
            {
                return File.ReadAllText(filePath);
            }

            public bool Exists(string filePath)
            {
                return File.Exists(filePath);
            }
        }
    }
}
