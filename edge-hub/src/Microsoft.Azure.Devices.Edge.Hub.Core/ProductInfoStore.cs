// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IProductInfoStore
    {
        Task SetProductInfo(string id, string productInfo);

        Task<string> GetProductInfo(string id);
    }

    public class ProductInfoStore : IProductInfoStore
    {
        readonly IKeyValueStore<string, string> productInfoEntityStore;

        public ProductInfoStore(IKeyValueStore<string, string> productInfoEntityStore)
        {
            this.productInfoEntityStore = Preconditions.CheckNotNull(productInfoEntityStore, nameof(productInfoEntityStore));
        }

        public Task SetProductInfo(string id, string productInfo)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            return this.productInfoEntityStore.Put(id, productInfo);
        }

        public async Task<string> GetProductInfo(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<string> productInfo = await this.productInfoEntityStore.Get(id);
            return productInfo.GetOrElse(string.Empty);
        }
    }
}
