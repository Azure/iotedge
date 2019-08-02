// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class StorageConfiguration
    {
        public StorageConfiguration(Option<int> maxBytes, Option<int> maxPercentage, bool deleteOlderMessages)
        {
            this.MaxBytes = maxBytes;
            this.MaxPercentage = maxPercentage;
            this.DeleteOlderMessages = deleteOlderMessages;
        }

        [JsonConstructor]
        StorageConfiguration(int? maxBytes, int? maxPercentage, bool? deleteOlderMessages)
            : this(Option.Maybe(maxBytes), Option.Maybe(maxPercentage), deleteOlderMessages ?? false)
        {
        }

        public Option<int> MaxBytes { get; }

        public Option<int> MaxPercentage { get; }

        public bool DeleteOlderMessages { get; }
    }
}
