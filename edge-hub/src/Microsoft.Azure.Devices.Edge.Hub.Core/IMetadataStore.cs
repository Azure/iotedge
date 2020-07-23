// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IMetadataStore
    {
        Task SetModelId(string id, string modelId);

        Task<Option<string>> GetModelId(string id);

        Task SetProductInfo(string id, string productInfo);

        Task<string> GetProductInfo(string id);

        Task<string> GetEdgeProductInfo(string id);

        Task<Option<ConnectionMetadata>> GetMetadata(string id);

        Task SetMetadata(string id, ConnectionMetadata metadata);
    }
}
