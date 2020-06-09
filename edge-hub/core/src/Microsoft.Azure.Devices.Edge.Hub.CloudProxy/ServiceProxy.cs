// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
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
        readonly IDeviceScopeApiClient securityScopesApiClient;

        public ServiceProxy(IDeviceScopeApiClient securityScopesApiClient)
        {
            this.securityScopesApiClient = Preconditions.CheckNotNull(securityScopesApiClient, nameof(securityScopesApiClient));
        }

        public IServiceIdentitiesIterator GetServiceIdentitiesIterator() => new ServiceIdentitiesIterator(this.securityScopesApiClient);

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId)
        {
            Option<ScopeResult> scopeResult = Option.None<ScopeResult>();
            try
            {
                ScopeResult res = await this.securityScopesApiClient.GetIdentity(deviceId, null);
                scopeResult = Option.Maybe(res);
                Events.IdentityScopeResultReceived(deviceId);
            }
            catch (DeviceScopeApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                Events.BadRequestResult(deviceId, ex.StatusCode);
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
                                Events.NullDevicesResult(deviceId);
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

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId)
        {
            string id = $"{deviceId}/{moduleId}";
            Option<ScopeResult> scopeResult = Option.None<ScopeResult>();
            try
            {
                ScopeResult res = await this.securityScopesApiClient.GetIdentity(deviceId, moduleId);
                scopeResult = Option.Maybe(res);
                Events.IdentityScopeResultReceived(id);
            }
            catch (DeviceScopeApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                Events.BadRequestResult(id, ex.StatusCode);
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
                                Events.NullDevicesResult(id);
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

            public static void NullDevicesResult(string id)
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, $"Received null devices in device scope result for {id}");
            }

            public static void NullResult()
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, "Received null device scope result");
            }

            public static void ScopeNotFound(string id)
            {
                Log.LogWarning((int)EventIds.NoScopeFound, $"Device scope not found for {id}. Parent-child relationship is not set.");
            }

            public static void BadRequestResult(string id, HttpStatusCode statusCode)
            {
                Log.LogDebug((int)EventIds.ScopeResultReceived, $"Received scope result for {id} with status code {statusCode} indicating that {id} has been removed from the scope");
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
                ScopeResult scopeResult = await this.continuationLink.Map(c => this.securityScopesApiClient.GetNext(c))
                    .GetOrElse(() => this.securityScopesApiClient.GetIdentitiesInScope());
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
