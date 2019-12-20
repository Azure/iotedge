// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;

    class StoreTestResultCollection<T> : ITestResultCollection<T>
    {
        readonly int batchSize;
        readonly ISequentialStore<T> store;
        readonly Queue<T> resultQueue;
        long lastLoadedPosition;
        T current;

        public StoreTestResultCollection(ISequentialStore<T> store, int batchSize)
        {
            this.batchSize = batchSize;
            this.store = store;
            this.resultQueue = new Queue<T>();
            this.lastLoadedPosition = -1;
        }

        public T Current => this.current;

        public async Task<bool> MoveNextAsync()
        {
            bool hasValue = this.GetFromQueue();
            if (!hasValue)
            {
                this.lastLoadedPosition = await this.LoadBatchIntoQueueAsync();
                hasValue = this.GetFromQueue();
            }

            return hasValue;
        }

        public void Reset()
        {
            this.lastLoadedPosition = -1;
        }

        public void Dispose()
        {
            this.store.Dispose();
        }

        bool GetFromQueue()
        {
            if (this.resultQueue.Count > 0)
            {
                this.current = this.resultQueue.Dequeue();
                return true;
            }
            else
            {
                this.current = default(T);
                return false;
            }
        }

        async Task<long> LoadBatchIntoQueueAsync()
        {
            IEnumerable<(long, T)> batch = await this.store.GetBatch(this.lastLoadedPosition + 1, this.batchSize);
            long lastLoadedKey = this.lastLoadedPosition;

            foreach ((long, T) values in batch)
            {
                this.resultQueue.Enqueue(values.Item2);
                lastLoadedKey = values.Item1;
            }

            return lastLoadedKey;
        }
    }
}
