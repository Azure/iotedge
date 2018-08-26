// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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
            ScopeResult scopeResult = await this.securityScopesApiClient.GetIdentity(deviceId, null);
            if (scopeResult != null)
            {
                if (scopeResult.Devices != null)
                {
                    int count = scopeResult.Devices.Count();
                    if (count == 1)
                    {
                        ServiceIdentity serviceIdentity = scopeResult.Devices.First().ToServiceIdentity();
                        return Option.Some(serviceIdentity);
                    }
                    else
                    {
                        Events.UnexpectedResult(count, 1, "devices");
                    }
                }
                else
                {
                    Events.NullDevicesResult();
                }
            }
            else
            {
                Events.NullResult();
            }

            return Option.None<ServiceIdentity>();
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId)
        {
            ScopeResult scopeResult = await this.securityScopesApiClient.GetIdentity(deviceId, moduleId);
            if (scopeResult != null)
            {
                if (scopeResult.Modules != null)
                {
                    int count = scopeResult.Modules.Count();
                    if (count == 1)
                    {
                        ServiceIdentity serviceIdentity = scopeResult.Modules.First().ToServiceIdentity();
                        return Option.Some(serviceIdentity);
                    }
                    else
                    {
                        Events.UnexpectedResult(count, 1, "devices");
                    }
                }
                else
                {
                    Events.NullDevicesResult();
                }
            }
            else
            {
                Events.NullResult();
            }

            return Option.None<ServiceIdentity>();
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

            public bool HasNext { get; private set; }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ServiceProxy>();
            const int IdStart = CloudProxyEventIds.ServiceProxy;

            enum EventIds
            {
                IteratorCreated = IdStart,
                ScopeResultReceived,
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

            public static void UnexpectedResult(int count, int expected, string entityName)
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, $"Expected to receive {expected} {entityName} but received {count} instead");
            }

            public static void NullDevicesResult()
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, "Received null devices in device scope result");
            }

            public static void NullResult()
            {
                Log.LogWarning((int)EventIds.UnexpectedResult, "Received null device scope result");
            }
        }
    }
}
