// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Threading.Tasks;

    public interface ICheckpointerFactory
    {
        Task<ICheckpointer> CreateAsync(string id);
        Task<ICheckpointer> CreateAsync(string id, string endpointId, uint priority);
    }
}
