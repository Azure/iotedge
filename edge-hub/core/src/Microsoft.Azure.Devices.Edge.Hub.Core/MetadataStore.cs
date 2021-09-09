// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class MetadataStore : IMetadataStore
    {
        readonly IKeyValueStore<string, string> metadataEntityStore;
        readonly string edgeProductInfo;

        public MetadataStore(IKeyValueStore<string, string> metadataEntityStore, string edgeProductInfo)
        {
            this.metadataEntityStore = Preconditions.CheckNotNull(metadataEntityStore);
            this.edgeProductInfo = Preconditions.CheckNotNull(edgeProductInfo, nameof(edgeProductInfo));
        }

        public async Task<ConnectionMetadata> GetMetadata(string id)
        {
            Option<string> value = await this.metadataEntityStore.Get(id);
            return await value.Match(
                async v =>
                {
                    return await this.GetOrMigrateConnectionMetadata(id, v);
                },
                () =>
                {
                    return Task.FromResult(new ConnectionMetadata(this.edgeProductInfo));
                });
        }

        async Task<ConnectionMetadata> GetOrMigrateConnectionMetadata(string id, string entityValue)
        {
            if (this.TryDeserialize(entityValue, out ConnectionMetadata metadata))
            {
                return metadata;
            }
            else
            {
                // Perform the migration by setting the new metadata object instead of the old productInfo string
                await this.SetMetadata(id, metadata);
                return metadata;
            }
        }

        bool TryDeserialize(string entityValue, out ConnectionMetadata metadata)
        {
            try
            {
                metadata = JsonConvert.DeserializeObject<ConnectionMetadata>(entityValue);
                return true;
            }
            catch (JsonException)
            {
                // If deserialization fails, assume the string is an old productInfo.
                // We must do this only for migration purposes, since this store used to just be a productInfoStore.
                metadata = new ConnectionMetadata(entityValue, this.edgeProductInfo);
                return false;
            }
        }

        async Task SetMetadata(string id, ConnectionMetadata metadata) => await this.metadataEntityStore.Put(id, JsonConvert.SerializeObject(metadata));

        public async Task SetMetadata(string id, string productInfo, Option<string> modelId)
        {
            ConnectionMetadata metadata = new ConnectionMetadata(productInfo, modelId, this.edgeProductInfo);
            await this.metadataEntityStore.Put(id, JsonConvert.SerializeObject(metadata));
        }

        public async Task SetModelId(string id, string modelId)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                ConnectionMetadata oldOrEmptyMetadata = await this.GetMetadata(id);
                ConnectionMetadata newMetadata = new ConnectionMetadata(oldOrEmptyMetadata.ProductInfo, Option.Some(modelId), this.edgeProductInfo);
                await this.SetMetadata(id, newMetadata);
            }
        }

        public async Task SetProductInfo(string id, string productInfo)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            ConnectionMetadata oldOrEmptyMetadata = await this.GetMetadata(id);
            ConnectionMetadata newMetadata = new ConnectionMetadata(productInfo, oldOrEmptyMetadata.ModelId, this.edgeProductInfo);
            await this.SetMetadata(id, newMetadata);
        }
    }
}
