// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ProductInfoStore : IProductInfoStore
    {
        readonly IKeyValueStore<string, string> productInfoEntityStore;
        readonly string edgeProductInfo;

        public ProductInfoStore(IKeyValueStore<string, string> productInfoEntityStore, string edgeProductInfo)
        {
            this.productInfoEntityStore = Preconditions.CheckNotNull(productInfoEntityStore, nameof(productInfoEntityStore));
            this.edgeProductInfo = Preconditions.CheckNotNull(edgeProductInfo, nameof(edgeProductInfo));
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

        public async Task<string> GetEdgeProductInfo(string id)
        {
            string clientProductInfo = await this.GetProductInfo(id);
            string edgeProductInfo = $"{clientProductInfo} {this.edgeProductInfo}".Trim();
            return edgeProductInfo;
        }
    }
}
