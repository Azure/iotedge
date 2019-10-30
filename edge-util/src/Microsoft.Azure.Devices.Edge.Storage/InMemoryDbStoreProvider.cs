// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class InMemoryDbStoreProvider : IDbStoreProvider
    {
        const string DefaultPartitionName = "$Default";
        readonly ConcurrentDictionary<string, IDbStore> partitionDbStoreDictionary = new ConcurrentDictionary<string, IDbStore>();

        public IDbStore GetDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            IDbStore dbStore = this.partitionDbStoreDictionary.GetOrAdd(partitionName, new InMemoryDbStore());
            return dbStore;
        }

        public IDbStore GetDbStore() => this.GetDbStore(DefaultPartitionName);

        public void RemoveDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            this.partitionDbStoreDictionary.TryRemove(partitionName, out IDbStore _);
        }

        public void Dispose()
        {
            // No-op
        }

        public void RemoveDbStore() => this.RemoveDbStore(DefaultPartitionName);

        public Task CloseAsync()
        {
            // No-op
            return Task.CompletedTask;
        }
    }
}
