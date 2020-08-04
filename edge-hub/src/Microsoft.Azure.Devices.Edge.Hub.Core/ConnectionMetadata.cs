// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ConnectionMetadata
    {
        [JsonConstructor]
        public ConnectionMetadata(string productInfo, string modelId, string edgeProductInfo)
        {
            Preconditions.CheckNonWhiteSpace(edgeProductInfo, nameof(edgeProductInfo));
            this.ModelId = Option.Maybe(modelId);
            this.ProductInfo = productInfo;
            this.EdgeProductInfo = edgeProductInfo;
        }

        public ConnectionMetadata(string productInfo, Option<string> modelId, string edgeProductInfo)
        {
            Preconditions.CheckNonWhiteSpace(edgeProductInfo, nameof(edgeProductInfo));
            this.ModelId = modelId;
            this.ProductInfo = productInfo;
            this.EdgeProductInfo = this.BuildEdgeProductInfo(productInfo, edgeProductInfo);
        }

        public ConnectionMetadata(string productInfo, string edgeProductInfo)
        {
            Preconditions.CheckNonWhiteSpace(edgeProductInfo, nameof(edgeProductInfo));
            this.ProductInfo = productInfo;
            this.ModelId = Option.None<string>();
            this.EdgeProductInfo = this.BuildEdgeProductInfo(productInfo, edgeProductInfo);
        }

        public ConnectionMetadata(string edgeProductInfo)
        {
            Preconditions.CheckNonWhiteSpace(edgeProductInfo, nameof(edgeProductInfo));
            this.EdgeProductInfo = this.BuildEdgeProductInfo(string.Empty, edgeProductInfo);
            // To be backward compatible, set productInfo to be empty if it doesn't exist
            this.ProductInfo = string.Empty;
        }

        string BuildEdgeProductInfo(string clientProductInfo, string edgeProductInfo)
        {
            return $"{clientProductInfo} {edgeProductInfo}".Trim();
        }

        [JsonConverter(typeof(OptionConverter<string>))]
        [JsonProperty("ModelId")]
        public Option<string> ModelId { get; }

        [JsonProperty("ProductInfo")]
        public string ProductInfo { get; }

        [JsonProperty("EdgeProductInfo")]
        public string EdgeProductInfo { get; }
    }
}
