// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using RocksDbSharp;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RocksDbOptionsProvider : IRocksDbOptionsProvider
    {
        // Some explanations from
        // https://github.com/facebook/rocksdb/blob/master/include/rocksdb/options.h
        // and
        // https://github.com/facebook/rocksdb/blob/master/include/rocksdb/advanced_options.h
        // These defaults were taken from ColumnFamilyOptions::OptimizeForSmallDb()

        // write_buffer_size = 2M (bytes) per column family
        // Amount of data to build up in memory (backed by an unsorted log
        // on disk) before converting to a sorted on-disk file.
        // Set to limit total memory usage.
        // 
        const ulong WriteBufferSize = 2 * 1024 * 1024;
        //
        // target_file_size_base = 2M (bytes) per column family
        // Target file size for compaction
        // Set to limit "max_compaction_bytes" and file sizes
        //
        const ulong TargetFileSizeBase = 2UL * 1024UL * 1024UL;
        //
        // max_bytes_for_level_base = 10M (bytes) per column family
        // Control maximum total data size for a level.
        // Set to limit file sizes
        //
        const ulong MaxBytesForLevelBase = 10UL * 1024UL * 1024UL;
        //
        // soft_pending_compaction_bytes_limit = 10M (bytes) per column family.
        // All writes will be slowed down to at least delayed_write_rate if estimated
        // bytes needed to be compaction exceed this threshold
        // Set to approx 1/10 hard limit
        //
        const ulong SoftPendingCompactionBytes = 10UL * 1024UL * 1024UL;
        //
        // hard_pending_compaction_bytes_limit = 1G (bytes) per column family
        // All writes are stopped if estimated bytes needed to be compaction exceed
        // this threshold.
        // Set to a limit less than a 32 bit #
        //
        const ulong HardPendingCompactionBytes = 1024UL * 1024UL * 1024UL;

        readonly ISystemEnvironment env;
        readonly bool optimizeForPerformance;

        public RocksDbOptionsProvider(ISystemEnvironment env, bool optimizeForPerformance)
        {
            this.env = Preconditions.CheckNotNull(env);
            this.optimizeForPerformance = optimizeForPerformance;
        }

        public DbOptions GetDbOptions()
        {
            DbOptions options = new DbOptions()
                .SetCreateIfMissing()
                .SetCreateMissingColumnFamilies();

            if (this.env.Is32BitProcess || !this.optimizeForPerformance)
            {
                // restrict some sizes if 32 bit OS.
                options.SetWriteBufferSize(WriteBufferSize);
                options.SetTargetFileSizeBase(TargetFileSizeBase);
                options.SetMaxBytesForLevelBase(MaxBytesForLevelBase);
                options.SetSoftPendingCompactionBytesLimit(SoftPendingCompactionBytes);
                options.SetHardPendingCompactionBytesLimit(HardPendingCompactionBytes);
            }

            return options;
        }

        public ColumnFamilyOptions GetColumnFamilyOptions()
        {
            var options = new ColumnFamilyOptions();

            if (this.env.Is32BitProcess)
            {
                // restrict some sizes if 32 bit OS.
                options.SetWriteBufferSize(WriteBufferSize);
                options.SetTargetFileSizeBase(TargetFileSizeBase);
                options.SetMaxBytesForLevelBase(MaxBytesForLevelBase);
                options.SetSoftPendingCompactionBytesLimit(SoftPendingCompactionBytes);
                options.SetHardPendingCompactionBytesLimit(HardPendingCompactionBytes);
            }
            return options;
        }
    }
}
