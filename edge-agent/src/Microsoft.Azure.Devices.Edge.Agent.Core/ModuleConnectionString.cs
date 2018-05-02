// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConnectionString
    {
        const string HostNamePropertyName = "HostName";
        const string GatewayHostNamePropertyName = "GatewayHostName";
        const string DeviceIdPropertyName = "DeviceId";
        const string ModuleIdPropertyname = "ModuleId";
        const string SharedAccessKeyPropertyName = "SharedAccessKey";
        const char ValuePairDelimiter = ';';

        readonly string sasKey;

        ModuleConnectionString(string iotHubHostName, string gatewayHostName, string deviceId, string moduleId, string sasKey)
        {
            this.IotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.GatewayHostName = gatewayHostName;
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.sasKey = sasKey;
        }

        public string IotHubHostName { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public string GatewayHostName { get; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.ModuleId))
            {
                throw new ArgumentException("Required parameter moduleId has not been set");
            }

            var connectionString = new StringBuilder();
            this.AppendIfNotEmpty(connectionString, HostNamePropertyName, this.IotHubHostName);
            this.AppendIfNotEmpty(connectionString, DeviceIdPropertyName, this.DeviceId);
            this.AppendIfNotEmpty(connectionString, ModuleIdPropertyname, this.ModuleId);
            this.AppendIfNotEmpty(connectionString, SharedAccessKeyPropertyName, this.sasKey);
            this.AppendIfNotEmpty(connectionString, GatewayHostNamePropertyName, this.GatewayHostName);
            return connectionString.ToString();
        }

        void AppendIfNotEmpty(StringBuilder stringBuilder, string propertyName, string propertyValue)
        {
            if (!string.IsNullOrEmpty(propertyValue))
            {
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(ValuePairDelimiter);
                }

                stringBuilder.Append($"{propertyName}={propertyValue}");
            }
        }

        public static implicit operator string(ModuleConnectionString moduleConnectionStringBuilder) => moduleConnectionStringBuilder.ToString();

        public class ModuleConnectionStringBuilder
        {
            readonly string iotHubHostName;
            readonly string deviceId;
            string moduleId;
            string gatewayHostname;
            string sasKey;

            public ModuleConnectionStringBuilder(string iotHubHostName, string deviceId)
            {
                this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
                this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            }

            public ModuleConnectionStringBuilder WithModuleId(string moduleId)
            {
                this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
                return this;
            }

            public ModuleConnectionStringBuilder WithGatewayHostName(string gatewayHostName)
            {
                this.gatewayHostname = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
                return this;
            }

            public ModuleConnectionStringBuilder WithSharedAccessKey(string sasKey)
            {
                this.sasKey = Preconditions.CheckNonWhiteSpace(sasKey, nameof(sasKey));
                return this;
            }

            public ModuleConnectionString Build() => new ModuleConnectionString(this.iotHubHostName, this.gatewayHostname, this.deviceId, this.moduleId, this.sasKey);
        }
    }
}
