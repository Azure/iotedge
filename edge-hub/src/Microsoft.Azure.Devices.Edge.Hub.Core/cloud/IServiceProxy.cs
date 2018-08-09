// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ServiceAuthenticationType
    {
        SasKey,
        CertificateThumbprint,
        CertificateAuthority,
        None,
    }

    public interface IServiceProxy
    {
        //Task<IEnumerable<string>> GetDevicesInScope();

        //Task<ServiceIdentity> GetDevice(string deviceId);

        //Task<IEnumerable<ServiceIdentity>> GetModulesOnDevice(string deviceId);

        ISecurityScopeIdentitiesIterator GetSecurityScopeIdentitiesIterator();
    }

    public interface ISecurityScopeIdentitiesIterator
    {
        Task<IEnumerable<ServiceIdentity>> GetNext();

        bool HasNext { get; }
    }

    public class ServiceIdentity
    {
        public ServiceIdentity(string deviceId, string moduleId, bool isEdgeDevice, ServiceAuthentication serviceAuthentication)
        {
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
            this.IsEdgeDevice = isEdgeDevice;
            this.Authentication = serviceAuthentication;
            this.Id = string.IsNullOrWhiteSpace(moduleId) ? deviceId : $"{deviceId}/{moduleId}";
        }

        [JsonIgnore]
        public string Id { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public bool IsEdgeDevice { get; }

        public bool IsModule => !string.IsNullOrWhiteSpace(this.ModuleId);

        public ServiceAuthentication Authentication { get; }
    }

    public class ServiceAuthentication
    {
        public ServiceAuthentication(ServiceAuthenticationType type, SymmetricKey symmetricKey, X509Thumbprint x509Thumbprint)
        {
            this.Type = type;
            this.SymmetricKey = symmetricKey;
            this.X509Thumbprint = x509Thumbprint;
        }

        public ServiceAuthenticationType Type { get; }

        public SymmetricKey SymmetricKey { get; }

        public X509Thumbprint X509Thumbprint { get; }
    }

    public class SymmetricKey
    {
        public SymmetricKey(string primaryKey, string secondaryKey)
        {
            this.PrimaryKey = primaryKey;
            this.SecondaryKey = secondaryKey;
        }

        public string PrimaryKey { get; }

        public string SecondaryKey { get; }
    }

    public class X509Thumbprint
    {
        public X509Thumbprint(string primaryThumbprint, string secondaryThumbprint)
        {
            this.PrimaryThumbprint = primaryThumbprint;
            this.SecondaryThumbprint = secondaryThumbprint;
        }

        public string PrimaryThumbprint { get; }

        public string SecondaryThumbprint { get; }
    }
}
