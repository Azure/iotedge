// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ConnectionMetadata
    {
        [JsonConstructor]
        public ConnectionMetadata(string productInfo, string modelId)
        {
            this.ModelId = Option.Maybe(modelId);
            this.ProductInfo = productInfo;
        }

        public ConnectionMetadata()
        {
        }

        [JsonConverter(typeof(OptionConverter<string>))]
        [JsonProperty("ModelId")]
        public Option<string> ModelId { get; set; }

        [JsonProperty("ProductInfo")]
        public string ProductInfo { get; set; }
    }
}
