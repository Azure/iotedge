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
        readonly Option<IStorageSpaceChecker> memoryStorageSpaceChecker;

        public InMemoryDbStoreProvider()
            : this(Option.None<IStorageSpaceChecker>())
        {
        }

        public InMemoryDbStoreProvider(Option<IStorageSpaceChecker> memoryStorageSpaceChecker)
        {
            this.memoryStorageSpaceChecker = memoryStorageSpaceChecker;
            this.memoryStorageSpaceChecker.ForEach(m => m.SetStorageUsageComputer(this.GetTotalMemoryUsage));
        }

        static IDbStore BuildInMemoryDbStore(Option<IStorageSpaceChecker> memoryStorageSpaceChecker)
            => memoryStorageSpaceChecker
                .Map(d => new StorageSpaceAwareDbStore(new InMemoryDbStore(), d) as IDbStore)
                .GetOrElse(new InMemoryDbStore() as IDbStore);

        public IDbStore GetDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            IDbStore dbStore = this.partitionDbStoreDictionary.GetOrAdd(partitionName, BuildInMemoryDbStore(this.memoryStorageSpaceChecker));
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

        long GetTotalMemoryUsage()
        {
            long dbSizeInBytes = 0;
            foreach (var inMemoryDbStore in this.partitionDbStoreDictionary)
            {
                if (inMemoryDbStore.Value is ISizedDbStore)
                {
                    dbSizeInBytes += ((ISizedDbStore)inMemoryDbStore.Value).DbSizeInBytes;
                }
            }

            return dbSizeInBytes;
        }
    }
}
