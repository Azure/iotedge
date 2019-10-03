// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Timer;
    using Microsoft.Azure.Devices.Edge.Util;
    using RocksDbSharp;

    class ColumnFamilyDbStore : IDbStore
    {
        readonly IRocksDb db;

        public ColumnFamilyDbStore(IRocksDb db, ColumnFamilyHandle handle)
        {
            this.db = Preconditions.CheckNotNull(db, nameof(db));
            this.Handle = Preconditions.CheckNotNull(handle, nameof(handle));
        }

        internal ColumnFamilyHandle Handle { get; }

        public Task Put(byte[] key, byte[] value) => this.Put(key, value, CancellationToken.None);

        public Task<Option<byte[]>> Get(byte[] key) => this.Get(key, CancellationToken.None);

        public Task Remove(byte[] key) => this.Remove(key, CancellationToken.None);

        public Task<bool> Contains(byte[] key) => this.Contains(key, CancellationToken.None);

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry() => this.GetFirstEntry(CancellationToken.None);

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry() => this.GetLastEntry(CancellationToken.None);

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback) => this.IterateBatch(batchSize, perEntityCallback, CancellationToken.None);

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback) => this.IterateBatch(startKey, batchSize, perEntityCallback, CancellationToken.None);

        public async Task<Option<byte[]>> Get(byte[] key, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(key, nameof(key));

            Option<byte[]> returnValue;
            using (MetricsV0.DbGetLatency("all"))
            {
                Func<byte[]> operation = () => this.db.Get(key, this.Handle);
                byte[] value = await operation.ExecuteUntilCancelled(cancellationToken);
                returnValue = value != null ? Option.Some(value) : Option.None<byte[]>();
            }

            return returnValue;
        }

        public Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            Preconditions.CheckNotNull(value, nameof(value));

            using (MetricsV0.DbPutLatency("all"))
            {
                Action operation = () => this.db.Put(key, value, this.Handle);
                return operation.ExecuteUntilCancelled(cancellationToken);
            }
        }

        public Task Remove(byte[] key, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            Action operation = () => this.db.Remove(key, this.Handle);
            return operation.ExecuteUntilCancelled(cancellationToken);
        }

        public async Task<Option<(byte[] key, byte[] value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            using (Iterator iterator = this.db.NewIterator(this.Handle))
            {
                Action operation = () => iterator.SeekToLast();
                await operation.ExecuteUntilCancelled(cancellationToken);
                if (iterator.Valid())
                {
                    byte[] key = iterator.Key();
                    byte[] value = iterator.Value();
                    return Option.Some((key, value));
                }
                else
                {
                    return Option.None<(byte[], byte[])>();
                }
            }
        }

        public async Task<Option<(byte[] key, byte[] value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            using (Iterator iterator = this.db.NewIterator(this.Handle))
            {
                Action operation = () => iterator.SeekToFirst();
                await operation.ExecuteUntilCancelled(cancellationToken);
                if (iterator.Valid())
                {
                    byte[] key = iterator.Key();
                    byte[] value = iterator.Value();
                    return Option.Some((key, value));
                }
                else
                {
                    return Option.None<(byte[], byte[])>();
                }
            }
        }

        public async Task<bool> Contains(byte[] key, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            Func<byte[]> operation = () => this.db.Get(key, this.Handle);
            byte[] value = await operation.ExecuteUntilCancelled(cancellationToken);
            return value != null;
        }

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(startKey, nameof(startKey));
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
            Preconditions.CheckNotNull(callback, nameof(callback));

            return this.IterateBatch(iterator => iterator.Seek(startKey), batchSize, callback, cancellationToken);
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
            Preconditions.CheckNotNull(callback, nameof(callback));

            return this.IterateBatch(iterator => iterator.SeekToFirst(), batchSize, callback, cancellationToken);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Don't dispose the Db here as we don't know if the caller
                // meant to dispose just the ColumnFamilyDbStore or the DB.
                // this.db?.Dispose();
            }
        }

        async Task IterateBatch(Action<Iterator> seeker, int batchSize, Func<byte[], byte[], Task> callback, CancellationToken cancellationToken)
        {
            // Use tailing iterator to prevent creating a snapshot.
            var readOptions = new ReadOptions();
            readOptions.SetTailing(true);

            using (Iterator iterator = this.db.NewIterator(this.Handle, readOptions))
            {
                int counter = 0;
                for (seeker(iterator); iterator.Valid() && counter < batchSize; iterator.Next(), counter++)
                {
                    byte[] key = iterator.Key();
                    byte[] value = iterator.Value();
                    await callback(key, value);
                }
            }
        }

        static class MetricsV0
        {
            static readonly TimerOptions DbPutLatencyOptions = new TimerOptions
            {
                Name = "DbPutLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            static readonly TimerOptions DbGetLatencyOptions = new TimerOptions
            {
                Name = "DbGetLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            public static IDisposable DbPutLatency(string identity) => Util.Metrics.MetricsV0.Latency(GetTags(identity), DbPutLatencyOptions);

            public static IDisposable DbGetLatency(string identity) => Util.Metrics.MetricsV0.Latency(GetTags(identity), DbGetLatencyOptions);

            static MetricTags GetTags(string id)
            {
                return new MetricTags("EndpointId", id);
            }
        }
    }
}
