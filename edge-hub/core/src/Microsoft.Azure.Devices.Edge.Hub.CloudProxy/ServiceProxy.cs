// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ServiceProxy : IServiceProxy
    {
        readonly IDeviceScopeApiClientProvider securityScopesApiClientProvider;
        readonly bool nestedEdgeEnabled;

        public ServiceProxy(IDeviceScopeApiClientProvider securityScopesApiClientProvider, bool nestedEdgeEnabled = true)
        {
            this.securityScopesApiClientProvider = Preconditions.CheckNotNull(securityScopesApiClientProvider, nameof(securityScopesApiClientProvider));
            this.nestedEdgeEnabled = nestedEdgeEnabled;
        }

        public IServiceIdentitiesIterator GetServiceIdentitiesIterator()
        {
            if (this.nestedEdgeEnabled)
            {
                return new NestedServiceIdentitiesIterator(this.securityScopesApiClientProvider);
            }
            else
            {
                return new ServiceIdentitiesIterator(this.securityScopesApiClientProvider.CreateDeviceScopeClient());
            }
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string onBehalfOfDevice)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Preconditions.CheckNonWhiteSpace(onBehalfOfDevice, nameof(onBehalfOfDevice));

            Option<ScopeResult> scopeResult = Option.None<ScopeResult>();
            try
            {
                IDeviceScopeApiClient client;
                ScopeResult res;

                if (this.nestedEdgeEnabled)
                {
                    client = this.securityScopesApiClientProvider.CreateNestedDeviceScopeClient();
                    res = await client.GetIdentityOnBehalfOfAsync(deviceId, Option.None<string>(), onBehalfOfDevice);
                }
                else
                {
                    client = this.securityScopesApiClientProvider.CreateDeviceScopeClient();
                    res = await client.GetIdentityAsync(deviceId, null);
                }

                scopeResult = Option.Maybe(res);
                Events.IdentityScopeResultReceived(deviceId);
            }
            catch (DeviceScopeApiException ex)
            {
                Events.ErrorRequestResult(deviceId, ex.StatusCode);
                throw this.MapException(ex);
            }

            Option<ServiceIdentity> serviceIdentityResult =
                scopeResult
                    .Map(
                        sc =>
                        {
                            if (sc.Devices != null)
                            {
                                int count = sc.Devices.Count();
                                if (count == 1)
                                {
                                    ServiceIdentity serviceIdentity = sc.Devices.First().ToServiceIdentity();
                                    return Option.Some(serviceIdentity);
                                }
                                else
                                {
                                    Events.UnexpectedResult(count, 1, "devices", deviceId);
                                }
                            }
                            else
                            {
                                Events.NoResult(deviceId);
                            }

                            return Option.None<ServiceIdentity>();
                        })
                    .GetOrElse(
                        () =>
                        {
                            Events.ScopeNotFound(deviceId);
                            return Option.None<ServiceIdentity>();
                        });

            return serviceIdentityResult;
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId, string onBehalfOfDevice)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(onBehalfOfDevice, nameof(onBehalfOfDevice));

            string id = $"{deviceId}/{moduleId}";
            Option<ScopeResult> scopeResult = Option.None<ScopeResult>();

            try
            {
                IDeviceScopeApiClient client;
                ScopeResult res;

                if (this.nestedEdgeEnabled)
                {
                    client = this.securityScopesApiClientProvider.CreateNestedDeviceScopeClient();
                    res = await client.GetIdentityOnBehalfOfAsync(deviceId, Option.Some(moduleId), onBehalfOfDevice);
                }
                else
                {
                    client = this.securityScopesApiClientProvider.CreateDeviceScopeClient();
                    res = await client.GetIdentityAsync(deviceId, moduleId);
                }

                scopeResult = Option.Maybe(res);
                Events.IdentityScopeResultReceived(id);
            }
            catch (DeviceScopeApiException ex)
            {
                Events.ErrorRequestResult(deviceId, ex.StatusCode);
                throw this.MapException(ex);
            }

            Option<ServiceIdentity> serviceIdentityResult =
                scopeResult
                    .Map(
                        sc =>
                        {
                            if (sc.Modules != null)
                            {
                                int count = sc.Modules.Count();
                                if (count == 1)
                                {
                                    ServiceIdentity serviceIdentity = sc.Modules.First().ToServiceIdentity();
                                    return Option.Some(serviceIdentity);
                                }
                                else
                                {
                                    Events.UnexpectedResult(count, 1, "modules", id);
                                }
                            }
                            else
                            {
                                Events.NoResult(id);
                            }

                            return Option.None<ServiceIdentity>();
                        })
                    .GetOrElse(
                        () =>
                        {
                            Events.ScopeNotFound(id);
                            return Option.None<ServiceIdentity>();
                        });

            return serviceIdentityResult;
        }

        Exception MapException(DeviceScopeApiException ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    return new DeviceInvalidStateException($"Device not in scope: [{ex.StatusCode}: {ex.Message}].", ex);
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.NotFound:
                    return new DeviceInvalidStateException($"Device not found: [{ex.StatusCode}: {ex.Message}].", ex);
                default:
                    return new TimeoutException($"Request failed: [{ex.StatusCode}: {ex.Message}].", ex);
            }
        }

        static class Events
        {
            const int IdStart = CloudProxyEventIds.ServiceProxy;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ServiceProxy>();

            enum EventIds
            {
                IteratorCreated = IdStart,
                ScopeResultReceived,
                NoScopeFound,
                UnexpectedResult
            }

            public static void IteratorCreated()
            {
                Log.LogDebug((int)EventIds.IteratorCreated, $"Created iterator to iterate all service identities in the scope of this IoT Edge device");
            }

            public static void ScopeResultReceived(ScopeResult scopeResult)
            {
                string continuationLinkExists = string.IsNullOrWhiteSpace(scopeResult.ContinuationLink) ? "null" : "valid";
                Log.LogDebug((int)EventIds.ScopeResultReceived, $"Received scope result with {scopeResult.Devices?.Count() ?? 0} devices, {scopeResult.Modules?.Count() ?? 0} modules and {continuationLinkExists} continuation link");
            }

            public static void IdentityScopeResultReceived(string id)
            {
                Log.LogDebug((int)EventIds.ScopeResultReceived, $"Received scope result for {id}");
            }

            public static void UnexpectedResult(int count, int expected, string entityName, string id)
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, $"Expected to receive {expected} {entityName} but received {count} instead, for {id}");
            }

            public static void NoResult(string id)
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, $"Received no identity in device scope result for {id}");
            }

            public static void NullResult()
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, "Received null device scope result");
            }

            public static void ScopeNotFound(string id)
            {
                Log.LogWarning((int)EventIds.NoScopeFound, $"Device scope not found for {id}. Parent-child relationship is not set.");
            }

            public static void ErrorRequestResult(string id, HttpStatusCode statusCode)
            {
                Log.LogDebug((int)EventIds.ScopeResultReceived, $"Received scope result for {id} with status code {statusCode}.");
            }
        }

        /// <summary>
        /// This is a helper class used to pull down the devices/modules in scope for every nested Edge Device.
        /// It maintains a queue of IDeviceScopeApiClient, each representating a child Edge device
        /// in the subtree, and uses them to perform breadth-first-expansion on the tree.
        /// </summary>
        class NestedServiceIdentitiesIterator : IServiceIdentitiesIterator
        {
            IDeviceScopeApiClientProvider clientProvider;
            IDeviceScopeApiClient actorClient;
            Queue<IDeviceScopeApiClient> remainingEdgeNodes;

            public NestedServiceIdentitiesIterator(IDeviceScopeApiClientProvider securityScopesApiClientProvider)
            {
                this.clientProvider = Preconditions.CheckNotNull(securityScopesApiClientProvider);

                // Put the first node (the actor device) into the queue
                this.actorClient = this.clientProvider.CreateNestedDeviceScopeClient();
                this.remainingEdgeNodes = new Queue<IDeviceScopeApiClient>();
                this.remainingEdgeNodes.Enqueue(this.actorClient);
            }

            public bool HasNext => this.remainingEdgeNodes.Count > 0;

            public async Task<IEnumerable<ServiceIdentity>> GetNext()
            {
                // Check for the empty-case
                if (this.remainingEdgeNodes.Count == 0)
                {
                    return Enumerable.Empty<ServiceIdentity>();
                }

                // Move the pending nodes into a local copy
                IList<IDeviceScopeApiClient> nodes = this.remainingEdgeNodes.ToList();
                this.remainingEdgeNodes.Clear();

                // Make an upstream call for each of the remaining nodes in the queue
                var results = await Task.WhenAll(nodes.Select(node => this.GetScopeForDevice(node)));

                // Accumulate the multiple results into a single collection
                var serviceIdentities = results.Aggregate(
                    (acc, next) =>
                    {
                        acc.AddRange(next);
                        return acc;
                    });

                return serviceIdentities.AsEnumerable();
            }

            async Task<List<ServiceIdentity>> GetScopeForDevice(IDeviceScopeApiClient client)
            {
                var serviceIdentities = new List<ServiceIdentity>();

                // Make the call to upstream and fetch the next batch of identities
                ScopeResult scopeResult = await client.GetIdentitiesInScopeAsync();
                if (scopeResult != null)
                {
                    Events.ScopeResultReceived(scopeResult);
                    if (scopeResult.Devices != null)
                    {
                        serviceIdentities.AddRange(scopeResult.Devices.Select(d => d.ToServiceIdentity()));
                    }

                    if (scopeResult.Modules != null)
                    {
                        serviceIdentities.AddRange(scopeResult.Modules.Select(m => m.ToServiceIdentity()));
                    }

                    if (!string.IsNullOrWhiteSpace(scopeResult.ContinuationLink))
                    {
                        // Since there's a continuation link, we're not done enumerating
                        // the identities under the current device scope. Enqueue another
                        // item in the queue to handle the continuation
                        IDeviceScopeApiClient continuationClient = this.clientProvider.CreateOnBehalfOf(client.TargetEdgeDeviceId, Option.Some(scopeResult.ContinuationLink));
                        this.remainingEdgeNodes.Enqueue(continuationClient);
                    }
                }
                else
                {
                    Events.NullResult();
                }

                // Enqueue a new item for every child Edge in this batch.
                foreach (ServiceIdentity identity in serviceIdentities)
                {
                    // The current device itself will come back as part of the query,
                    // make sure we don't re-enqueue it again
                    if (identity.IsEdgeDevice && identity.DeviceId != client.TargetEdgeDeviceId)
                    {
                        IDeviceScopeApiClient childClient = this.clientProvider.CreateOnBehalfOf(identity.DeviceId, Option.None<string>());
                        this.remainingEdgeNodes.Enqueue(childClient);
                    }
                }

                return serviceIdentities;
            }
        }

        class ServiceIdentitiesIterator : IServiceIdentitiesIterator
        {
            readonly IDeviceScopeApiClient securityScopesApiClient;
            Option<string> continuationLink = Option.None<string>();

            public ServiceIdentitiesIterator(IDeviceScopeApiClient securityScopesApiClient)
            {
                this.securityScopesApiClient = Preconditions.CheckNotNull(securityScopesApiClient, nameof(securityScopesApiClient));
                this.HasNext = true;
                Events.IteratorCreated();
            }

            public bool HasNext { get; private set; }

            public async Task<IEnumerable<ServiceIdentity>> GetNext()
            {
                if (!this.HasNext)
                {
                    return Enumerable.Empty<ServiceIdentity>();
                }

                var serviceIdentities = new List<ServiceIdentity>();
                ScopeResult scopeResult = await this.continuationLink.Map(c => this.securityScopesApiClient.GetNextAsync(c))
                    .GetOrElse(() => this.securityScopesApiClient.GetIdentitiesInScopeAsync());
                if (scopeResult == null)
                {
                    Events.NullResult();
                }
                else
                {
                    Events.ScopeResultReceived(scopeResult);
                    if (scopeResult.Devices != null)
                    {
                        serviceIdentities.AddRange(scopeResult.Devices.Select(d => d.ToServiceIdentity()));
                    }

                    if (scopeResult.Modules != null)
                    {
                        serviceIdentities.AddRange(scopeResult.Modules.Select(m => m.ToServiceIdentity()));
                    }

                    if (!string.IsNullOrWhiteSpace(scopeResult.ContinuationLink))
                    {
                        this.continuationLink = Option.Some(scopeResult.ContinuationLink);
                        this.HasNext = true;
                    }
                    else
                    {
                        this.HasNext = false;
                    }
                }

                return serviceIdentities;
            }
        }
    }
}
