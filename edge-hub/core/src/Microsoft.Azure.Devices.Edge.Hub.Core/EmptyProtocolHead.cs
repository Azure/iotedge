// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class EmptyProtocolHead : IProtocolHead
    {
        static readonly IProtocolHead Instance = new EmptyProtocolHead();

        public static IProtocolHead GetInstance() => Instance;

        EmptyProtocolHead()
        {
        }

        public string Name => "EmptyProtocolHead";

        public Task CloseAsync(CancellationToken token) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task StartAsync() => Task.CompletedTask;
    }
}
