// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    public interface IModelIdStore
    {
        Task SetModelId(string id, string modelId);

        Task<string> GetModelId(string id);
    }
}
