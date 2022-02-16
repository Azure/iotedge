// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.IO;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public static class PersistentStorageValidation
    {
        public static void ValidateStorageIdentity(string storagePath, string deviceId, string iotHubHostname, string moduleId, Option<string> moduleGenerationId)
        {
            ILogger logger = SetupLogger();
            DeviceIdentity currentIdentity = new DeviceIdentity(deviceId, iotHubHostname, moduleId, moduleGenerationId);
            string filepath = Path.Combine(storagePath, "DEVICE_IDENTITY");
            string json = JsonConvert.SerializeObject(currentIdentity);

            if (!File.Exists(filepath))
            {
                File.WriteAllText(filepath, json);
            }
            else
            {
                DeviceIdentity savedIdentity = JsonConvert.DeserializeObject<DeviceIdentity>(File.ReadAllText(filepath));

                if (currentIdentity.Equals(savedIdentity))
                {
                    logger.LogInformation("Persistent storage is for a different device identity {actual}, then the current {current}. Deleting local storage.",  savedIdentity.DeviceId, currentIdentity.DeviceId);
                    Directory.Delete(storagePath, true);
                    Directory.CreateDirectory(storagePath);
                    File.WriteAllText(filepath, json);
                }
            }
        }

        static ILogger SetupLogger()
        {
            Logger.SetLogLevel("warning");
            ILogger logger = default(ILogger);
            return logger;
        }

        struct DeviceIdentity
        {
            public string DeviceId { get; }

            public string IotHubHostname { get; }

            public string ModuleId { get; }

            public string ModuleGenerationId { get; }

            public DeviceIdentity(string devId, string iothubHostname, string moduleId, Option<string> moduleGenId)
            {
                this.DeviceId = devId;
                this.IotHubHostname = iothubHostname;
                this.ModuleId = moduleId;
                this.ModuleGenerationId = moduleGenId.OrDefault();
            }
        }
    }
}
