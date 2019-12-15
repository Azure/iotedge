// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Nito.AsyncEx;

    class SimpleResults : IEnumerable<TestOperationResult>
    {
        readonly int batchSize;
        readonly ISequentialStore<TestOperationResult> store;

        public SimpleResults(string source, ISequentialStore<TestOperationResult> store, int batchSize)
        {
            this.batchSize = batchSize;
            this.Source = source;
            this.store = store;
        }

        public string Source { get; }

        public IEnumerator GetEnumerator()
        {
            return new Enumerator(this.store, this.Source, this.batchSize);
        }

        IEnumerator<TestOperationResult> IEnumerable<TestOperationResult>.GetEnumerator()
        {
            return new Enumerator(this.store, this.Source, this.batchSize);
        }

        public class Enumerator : IEnumerator<TestOperationResult>
        {
            readonly ISequentialStore<TestOperationResult> store;
            readonly int batchSize;
            readonly string source;
            readonly Queue<TestOperationResult> resultQueue;
            long lastLoadedPosition;
            TestOperationResult current;

            public Enumerator(ISequentialStore<TestOperationResult> store, string source, int batchSize)
            {
                this.store = store;
                this.lastLoadedPosition = -1;
                this.resultQueue = new Queue<TestOperationResult>();
                this.source = source;
                this.batchSize = batchSize;
            }

            object IEnumerator.Current => this.current;

            TestOperationResult IEnumerator<TestOperationResult>.Current => this.current;

            public bool MoveNext()
            {
                if (this.resultQueue.Count > 0)
                {
                    this.current = this.resultQueue.Dequeue();
                    return true;
                }

                //TODO: Net core 3.0 supports async enumerable, replace it when project is moved to 3.0
                this.lastLoadedPosition = AsyncContext.Run(this.LoadBatchIntoQueueAsync);

                if (this.resultQueue.Count > 0)
                {
                    this.current = this.resultQueue.Dequeue();
                    return true;
                }

                this.current = null;
                return false;
            }

            public void Reset()
            {
                this.lastLoadedPosition = -1;
            }

            public void Dispose()
            {
                this.store.Dispose();
            }

            async Task<long> LoadBatchIntoQueueAsync()
            {
                IEnumerable<(long, TestOperationResult)> batch = await this.store.GetBatch(this.lastLoadedPosition + 1, this.batchSize);
                long lastLoadedKey = this.lastLoadedPosition;

                foreach ((long, TestOperationResult) values in batch)
                {
                    if (!values.Item2.Source.Equals(this.source, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Result source is '{values.Item2.Source}' but expected should be '{this.source}'.");
                    }

                    this.resultQueue.Enqueue(values.Item2);
                    lastLoadedKey = values.Item1;
                }

                return lastLoadedKey;
            }
        }
    }
}
