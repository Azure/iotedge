// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;

    public class ServiceProxy : IServiceProxy
    {
        //public async Task<IEnumerable<string>> GetDevicesInScope()
        //{
        //    Module
        //}

        //public async Task<ServiceIdentity> GetDevice(string deviceId)
        //{
        //    throw new System.NotImplementedException();
        //}

        //public async Task<IEnumerable<ServiceIdentity>> GetIdentitiesInScope(string deviceId)
        //{
        //    throw new System.NotImplementedException();
        //}
        public ISecurityScopeIdentitiesIterator GetSecurityScopeIdentitiesIterator()
        {
            return new SecurityScopeIdentitiesIterator();
        }

        class SecurityScopeIdentitiesIterator : ISecurityScopeIdentitiesIterator
        {
            public Task<IEnumerable<ServiceIdentity>> GetNext()
            {

                return Task.FromResult(Enumerable.Empty<ServiceIdentity>());
            }
        }
    }
}
