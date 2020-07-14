// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModelIdStore
    {
        Task SetModelId(string id, string modelId);

        Task<Option<string>> GetModelId(string id);
    }
}
