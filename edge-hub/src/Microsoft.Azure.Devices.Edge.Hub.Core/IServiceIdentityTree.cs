// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IServiceIdentityTree
    {
        void InsertOrUpdate(ServiceIdentity identity);
        public void Remove(string id);
        public bool Contains(string id);
        public Option<ServiceIdentity> Get(string id);
        public IList<string> GetAllIds();
        public bool TryGetAuthChain(string id, out string authChain);
    }
}
