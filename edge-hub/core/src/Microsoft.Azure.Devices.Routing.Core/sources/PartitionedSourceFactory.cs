// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Sources
{
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class PartitionedSourceFactory : ISourceFactory
    {
        public static PartitionedSourceFactory Null { get; } = new NullPartitionedSourceFactory();

        public virtual Task<Source> CreateAsync(string hubName, Router router, CancellationToken token) =>
            this.CreateAsync(hubName, 0L, router, token);

        public abstract Task<Source> CreateAsync(string hubName, long partitionId, Router router, CancellationToken token);

        class NullPartitionedSourceFactory : PartitionedSourceFactory
        {
            public override Task<Source> CreateAsync(string hubName, long partitionId, Router router, CancellationToken token) =>
                Task.FromResult((Source)new NullSource(router));
        }
    }
}
