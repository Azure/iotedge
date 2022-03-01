// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public static class PersistentStorageValidation
    {
        public static bool ValidateStorageIdentity(string storagePath, string deviceId, string iotHubHostname, string moduleId, Option<string> moduleGenerationId, ILogger logger)
        {
            DeviceIdentity currentIdentity = new DeviceIdentity(deviceId, iotHubHostname, moduleId, moduleGenerationId.OrDefault());
            string filepath = Path.Combine(storagePath, "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(currentIdentity);

            if (!File.Exists(filepath))
            {
                File.WriteAllText(filepath, json);
                return true;
            }
            else
            {
                DeviceIdentity savedIdentity = JsonConvert.DeserializeObject<DeviceIdentity>(File.ReadAllText(filepath));

                if (!currentIdentity.Equals(savedIdentity))
                {
                    logger.LogInformation("Persistent storage is for a different device identity {actual} than the current identity {current}. Deleting local storage.", savedIdentity.DeviceId, currentIdentity.DeviceId);
                    Directory.Delete(storagePath, true);
                    Directory.CreateDirectory(storagePath);
                    File.WriteAllText(filepath, json);
                    return false;
                }

                return true;
            }
        }

        internal struct DeviceIdentity
        {
            [JsonProperty("deviceId")]
            public string DeviceId { get; }

            [JsonProperty("iotHubHostname")]
            public string IotHubHostname { get; }

            [JsonProperty("moduleId")]
            public string ModuleId { get; }

            [JsonProperty("moduleGenerationId")]
            public string ModuleGenerationId { get; }

            public DeviceIdentity(string deviceId, string iothubHostname, string moduleId, string moduleGenerationId)
            {
                this.DeviceId = deviceId;
                this.IotHubHostname = iothubHostname;
                this.ModuleId = moduleId;
                this.ModuleGenerationId = moduleGenerationId;
            }
        }
    }
}
