// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModelIdStore : IModelIdStore
    {
        readonly IKeyValueStore<string, string> modelIdEntityStore;

        public ModelIdStore(IKeyValueStore<string, string> modelIdEntityStore)
        {
            this.modelIdEntityStore = Preconditions.CheckNotNull(modelIdEntityStore, nameof(modelIdEntityStore));
        }

        public Task SetModelId(string id, string modelId)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            return !string.IsNullOrWhiteSpace(modelId) ? this.modelIdEntityStore.Put(id, modelId) : Task.CompletedTask;
        }

        public async Task<string> GetModelId(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<string> modelId = await this.modelIdEntityStore.Get(id);
            return modelId.GetOrElse(string.Empty);
        }
    }
}
