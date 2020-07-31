// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    public interface IProductInfoStore
    {
        Task SetProductInfo(string id, string productInfo);

        Task<string> GetProductInfo(string id);

        Task<string> GetEdgeProductInfo(string id);
    }
}
