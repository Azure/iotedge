// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Newtonsoft.Json;

    public class ConnectionMetadata
    {
        [JsonProperty("ModelId")]
        public string ModelId { get; set; }

        [JsonProperty("ProductInfo")]
        public string ProductInfo { get; set; }
    }
}
