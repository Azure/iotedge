// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IServiceProxy
    {
        Task<IEnumerable<string>> GetDevicesInScope();

        Task<ServiceIdentity> GetDevice(string deviceId);

        Task<IEnumerable<ServiceIdentity>> GetModulesOnDevice(string deviceId);
    }

    public class ServiceIdentity
    {
        public string DeviceId { get; }

        public string ModuleId { get; }

        public bool IsEdgeDevice { get; }

        public bool IsModule { get; }

        public ServiceAuthentication Authentication { get; }
    }

    public class ServiceAuthentication
    {
        public AuthenticationType Type { get; }

        public SymmetricKey SymmetricKey { get; }

        public X509Thumbprint X509Thumbprint { get; }
    }

    public class SymmetricKey
    {
        public string PrimaryKey { get; }

        public string SecondaryKey { get; }
    }
}
