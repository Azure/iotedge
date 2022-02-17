// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.IO;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public static class PersistentStorageValidation
    {
        public static void ValidateStorageIdentity(string storagePath, string deviceId, string iotHubHostname, string moduleId, Option<string> moduleGenerationId, ILogger logger)
        {
            DeviceIdentity currentIdentity = new DeviceIdentity(deviceId, iotHubHostname, moduleId, moduleGenerationId.OrDefault());
            string filepath = Path.Combine(storagePath, "DEVICE_IDENTITY.json");
            string json = JsonConvert.SerializeObject(currentIdentity);

            if (!File.Exists(filepath))
            {
                File.WriteAllText(filepath, json);
            }
            else
            {
                DeviceIdentity savedIdentity = JsonConvert.DeserializeObject<DeviceIdentity>(File.ReadAllText(filepath));

                if (!currentIdentity.Equals(savedIdentity))
                {
                    logger.LogInformation("Persistent storage is for a different device identity {actual} than the current identity {current}. Deleting local storage.",  savedIdentity.DeviceId, currentIdentity.DeviceId);
                    Directory.Delete(storagePath, true);
                    Directory.CreateDirectory(storagePath);
                    File.WriteAllText(filepath, json);
                }
            }
        }

        struct DeviceIdentity
        {
            public string DeviceId { get; set; }

            public string IotHubHostname { get; set; }

            public string ModuleId { get; set; }

            public string ModuleGenerationId { get; set; }

            public DeviceIdentity(string devId, string iothubHostname, string moduleId, string moduleGenId)
            {
                this.DeviceId = devId;
                this.IotHubHostname = iothubHostname;
                this.ModuleId = moduleId;
                this.ModuleGenerationId = moduleGenId;
            }
        }
    }
}
