// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
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

        public async Task<Option<ConnectionMetadata>> GetMetadata(string id)
        {
            Option<string> value = await this.metadataEntityStore.Get(id);
            return await value.Match(
                async v =>
                {
                    try
                    {
                        return Option.Some(JsonConvert.DeserializeObject<ConnectionMetadata>(v));
                    }
                    catch (JsonSerializationException)
                    {
                        // If deserialization fails, assume the string is an old productInfo.
                        // We must do this only for migration purposes, since this store used to just be a productInfoStore.
                        ConnectionMetadata metadata = new ConnectionMetadata() { ProductInfo = v };
                        await this.SetMetadata(id, metadata);
                        return Option.Maybe(metadata);
                    }
                },
                () =>
                {
                    return Task.FromResult(Option.None<ConnectionMetadata>());
                });
        }

        public async Task<Option<string>> GetModelId(string id)
        {
            Option<string> value = await this.metadataEntityStore.Get(id);
            return value.Match(
                v =>
                {
                    try
                    {
                        return Option.Some(JsonConvert.DeserializeObject<ConnectionMetadata>(v).ModelId);
                    }
                    catch (JsonSerializationException)
                    {
                        // If deserialization fails, assume the string is an old productInfo.
                        return Option.None<string>();
                    }
                },
                () => Option.None<string>());
        }

        public async Task<string> GetProductInfo(string id)
        {
            Option<string> value = await this.metadataEntityStore.Get(id);
            return await value.Match(
                async v =>
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<ConnectionMetadata>(v).ProductInfo;
                    }
                    catch (JsonSerializationException)
                    {
                        // If deserialization fails, assume the string is an old productInfo.
                        // We must do this only for migration purposes, since this store used to just be a productInfoStore.
                        ConnectionMetadata metadata = new ConnectionMetadata() { ProductInfo = v };
                        await this.metadataEntityStore.Put(id, JsonConvert.SerializeObject(metadata));
                        return v;
                    }
                },
                () => Task.FromResult(string.Empty));
        }

        public async Task SetMetadata(string id, ConnectionMetadata metadata) => await this.metadataEntityStore.Put(id, JsonConvert.SerializeObject(metadata));

        public async Task SetModelId(string id, string modelId)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                ConnectionMetadata metadata = (await this.GetMetadata(id)).GetOrElse(new ConnectionMetadata());
                metadata.ModelId = modelId;
                await this.metadataEntityStore.Put(id, JsonConvert.SerializeObject(metadata));
            }
        }

        public async Task SetProductInfo(string id, string productInfo)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            ConnectionMetadata metadata = (await this.GetMetadata(id)).GetOrElse(new ConnectionMetadata());
            metadata.ProductInfo = productInfo;
            await this.metadataEntityStore.Put(id, JsonConvert.SerializeObject(metadata));
        }

        public async Task<string> GetEdgeProductInfo(string id)
        {
            string clientProductInfo = await this.GetProductInfo(id);
            string edgeProductInfo = $"{clientProductInfo} {this.edgeProductInfo}".Trim();
            return edgeProductInfo;
        }
    }
}
