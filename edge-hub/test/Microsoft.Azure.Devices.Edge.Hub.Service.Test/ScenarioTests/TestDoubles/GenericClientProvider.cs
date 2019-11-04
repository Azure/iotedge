// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public abstract class GenericClientProvider : IClientProvider
    {
        private static readonly object lockObj = new object();

        // using concurrent dictionary because of its nicer interface, but every usage will be locked because of other racing operations
        private static ConcurrentDictionary<string, List<IClient>> providedClientInstances = new ConcurrentDictionary<string, List<IClient>>();
        private static ConcurrentDictionary<string, TaskCompletionSource<List<IClient>>> awaitedCreations = new ConcurrentDictionary<string, TaskCompletionSource<List<IClient>>>();

        public abstract IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings);
        public abstract IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings);
        public abstract IClient Create(IIdentity identity, ITokenProvider tokenProvider, ITransportSettings[] transportSettings);
        public abstract Task<IClient> CreateAsync(IIdentity identity, ITransportSettings[] transportSettings);

        public static async Task<List<IClient>> GetProvidedInstancesAsync(string id, CancellationToken cancellationToken)
        {
            var tcs = default(TaskCompletionSource<List<IClient>>);
            lock (lockObj)
            {
                var result = providedClientInstances.GetOrAdd(id, new List<IClient>());
                if (result.Count > 0)
                {
                    return result.ToList();
                }

                tcs = awaitedCreations.GetOrAdd(id, new TaskCompletionSource<List<IClient>>());
            }

            await Task.WhenAny(tcs.Task, cancellationToken.WhenCanceled());

            cancellationToken.ThrowIfCancellationRequested();

            return await tcs.Task;
        }

        public static async Task ExecuteOnLastOneAsync(string id, Func<IClient, Task<bool>> action, CancellationToken cancellationToken)
        {
            var lastOne = default(IClient);
            var succeeded = default(bool);
            var firstTry = true;

            do
            {
                if (!firstTry)
                {
                    await Task.Delay(100);
                }
                else
                {
                    firstTry = false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                lock (lockObj)
                {
                    var clients = providedClientInstances.GetOrAdd(id, new List<IClient>());
                    if (clients.Count > 0)
                    {
                        lastOne = clients.Last();
                    }
                    else
                    {
                        throw new InvalidOperationException($"Bad test setup: no IClient created yet for {id} to execute operations");
                    }
                }

                // it is possible that while this code is running, a new client gets created
                // in good case the action detects it (as the old client gets closed) so we can repeat
                succeeded = await action(lastOne);
            }
            while (!succeeded);
        }

        protected abstract IClient BuildClientInstance(IIdentity identity);

        protected IClient Create(IIdentity identity)
        {
            var result = this.BuildClientInstance(identity);

            lock (lockObj)
            {
                var createdInstances = providedClientInstances.GetOrAdd(identity.Id, new List<IClient>());
                createdInstances.Add(result);

                if (awaitedCreations.TryRemove(identity.Id, out TaskCompletionSource<List<IClient>> awaited))
                {
                    awaited.TrySetResult(createdInstances.ToList());
                }

                return result;
            }
        }
    }

    public class GenericClientProvider<T> : GenericClientProvider
        where T : IClientBuilder, new()
    {
        private T builder = new T();

        public override IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings) => this.Create(identity);
        public override IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings) => this.Create(identity);
        public override IClient Create(IIdentity identity, ITokenProvider tokenProvider, ITransportSettings[] transportSettings) => this.Create(identity);
        public override Task<IClient> CreateAsync(IIdentity identity, ITransportSettings[] transportSettings) => Task.FromResult(this.Create(identity) as IClient);

        public GenericClientProvider<T> WithBuilder(Func<T, T> builderDecorator)
        {
            builderDecorator(this.builder);
            return this;
        }

        protected override IClient BuildClientInstance(IIdentity identity)
        {
            return this.builder.Build(identity);
        }
    }
}
