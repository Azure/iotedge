// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IMetadataStore
    {
        Task SetModelId(string id, string modelId);

        Task SetProductInfo(string id, string productInfo);

        Task<ConnectionMetadata> GetMetadata(string id);

        Task SetMetadata(string id, string productInfo, Option<string> modelId);
    }
}
