// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a simple dictionary implementation of IServiceIdentityHierarchy
    /// that mirrors how the identity cache worked before nested Edge was added.
    /// </summary>
    public class ServiceIdentityDictionary : IServiceIdentityHierarchy
    {
        readonly string actorDeviceId;
        readonly Dictionary<string, ServiceIdentity> identities;

        public ServiceIdentityDictionary(string actorDeviceId)
        {
            this.actorDeviceId = Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
            this.identities = new Dictionary<string, ServiceIdentity>();
        }

        public string GetActorDeviceId() => this.actorDeviceId;

        public Task<Option<string>> GetAuthChain(string id) => Task.FromResult(Option.None<string>());

        public Task<Option<string>> GetEdgeAuthChain(string id) => throw new NotImplementedException("Nested Edge not enabled");

        public Task<IList<ServiceIdentity>> GetImmediateChildren(string id) => throw new NotImplementedException("Nested Edge not enabled");

        public Task InsertOrUpdate(ServiceIdentity identity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            this.identities[identity.Id] = identity;
            return Task.CompletedTask;
        }

        public Task Remove(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.identities.Remove(id);
            return Task.CompletedTask;
        }

        public Task<bool> Contains(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            return Task.FromResult(this.identities.ContainsKey(id));
        }

        public Task<Option<ServiceIdentity>> Get(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<ServiceIdentity> result = this.identities.TryGetValue(id, out ServiceIdentity identity)
                  ? Option.Some(identity)
                  : Option.None<ServiceIdentity>();
            return Task.FromResult(result);
        }

        public Task<IList<string>> GetAllIds()
        {
            IList<string> result = this.identities.Select(kvp => kvp.Value.Id).ToList();
            return Task.FromResult(result);
        }
    }
}
