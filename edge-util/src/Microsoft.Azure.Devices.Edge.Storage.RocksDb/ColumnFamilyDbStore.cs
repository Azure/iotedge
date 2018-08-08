// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Timer;
    using Microsoft.Azure.Devices.Edge.Storage;
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

        public Task<Option<byte[]>> Get(byte[] key)
        {
            Preconditions.CheckNotNull(key, nameof(key));

            Option<byte[]> returnValue;
            using (Metrics.DbGetLatency("all"))
            {
                byte[] value = this.db.Get(key, this.Handle);
                returnValue = value != null ? Option.Some(value) : Option.None<byte[]>();
            }
            return Task.FromResult(returnValue);
        }

        public Task Put(byte[] key, byte[] value)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            Preconditions.CheckNotNull(value, nameof(value));

            using (Metrics.DbPutLatency("all"))
            {
                this.db.Put(key, value, this.Handle);
            }
            return Task.CompletedTask;
        }

        public Task Remove(byte[] key)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            this.db.Remove(key, this.Handle);
            return Task.CompletedTask;
        }        

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry()
        {
            using (Iterator iterator = this.db.NewIterator(this.Handle))
            {
                iterator.SeekToLast();
                if (iterator.Valid())
                {
                    byte[] key = iterator.Key();
                    byte[] value = iterator.Value();
                    return Task.FromResult(Option.Some((key, value)));
                }
                else
                {
                    return Task.FromResult(Option.None<(byte[], byte[])>());
                }
            }
        }

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry()
        {
            using (Iterator iterator = this.db.NewIterator(this.Handle))
            {
                iterator.SeekToFirst();
                if (iterator.Valid())
                {
                    byte[] key = iterator.Key();
                    byte[] value = iterator.Value();
                    return Task.FromResult(Option.Some((key, value)));
                }
                else
                {
                    return Task.FromResult(Option.None<(byte[], byte[])>());
                }
            }
        }

        public Task<bool> Contains(byte[] key)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            byte[] value = this.db.Get(key, this.Handle);
            return Task.FromResult(value != null);
        }

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> callback)
        {
            Preconditions.CheckNotNull(startKey, nameof(startKey));
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
            Preconditions.CheckNotNull(callback, nameof(callback));

            return this.IterateBatch(iterator => iterator.Seek(startKey), batchSize, callback);
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> callback)
        {
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
            Preconditions.CheckNotNull(callback, nameof(callback));

            return this.IterateBatch(iterator => iterator.SeekToFirst(), batchSize, callback);
        }

        async Task IterateBatch(Action<Iterator> seeker, int batchSize, Func<byte[], byte[], Task> callback)
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

        static class Metrics
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

            internal static MetricTags GetTags(string id)
            {
                return new MetricTags("EndpointId", id);
            }

            public static IDisposable DbPutLatency(string identity) => Edge.Util.Metrics.Latency(GetTags(identity), DbPutLatencyOptions);

            public static IDisposable DbGetLatency(string identity) => Edge.Util.Metrics.Latency(GetTags(identity), DbGetLatencyOptions);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Don't dispose the Db here as we don't know if the caller
                // meant to dispose just the ColumnFamilyDbStore or the DB.
                //this.db?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
