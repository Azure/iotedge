// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Text;

    public class EdgeHubConnectionString
    {
        const string HostNamePropertyName = "HostName";
        const string GatewayHostNamePropertyName = "GatewayHostName";
        const string DeviceIdPropertyName = "DeviceId";
        const string ModuleIdPropertyname = "ModuleId";
        const string SharedAccessKeyPropertyName = "SharedAccessKey";
        const char ValuePairDelimiter = ';';

        EdgeHubConnectionString(string hostname, string gatewayHostname, string deviceId, string moduleId, string sharedAccessKey)
        {
            this.HostName = hostname;
            this.GatewayHostName = gatewayHostname;
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
            this.SharedAccessKey = sharedAccessKey;

            this.Validate();
        }

        public string HostName { get; }

        public string GatewayHostName { get; }

        public string SharedAccessKey { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public string ToConnectionString()
        {
            var connectionString = new StringBuilder();

            this.AppendIfNotEmpty(connectionString, HostNamePropertyName, this.HostName);
            this.AppendIfNotEmpty(connectionString, GatewayHostNamePropertyName, this.GatewayHostName);
            this.AppendIfNotEmpty(connectionString, DeviceIdPropertyName, this.DeviceId);
            this.AppendIfNotEmpty(connectionString, ModuleIdPropertyname, this.ModuleId);
            this.AppendIfNotEmpty(connectionString, SharedAccessKeyPropertyName, this.SharedAccessKey);

            // Remove last separator
            return connectionString.ToString().TrimEnd(new char[] { ValuePairDelimiter });
        }

        void Validate()
        {
            if (string.IsNullOrWhiteSpace(this.HostName))
            {
                throw new ArgumentException("IoT Hub hostname must be specified in connection string");
            }

            if (string.IsNullOrEmpty(this.DeviceId))
            {
                throw new ArgumentException("DeviceId must be specified in connection string");
            }

            if (string.IsNullOrEmpty(this.SharedAccessKey))
            {
                throw new ArgumentException("SharedAccessKey must be specified in connection string");
            }

            if (!string.IsNullOrEmpty(this.SharedAccessKey))
            {
                Convert.FromBase64String(this.SharedAccessKey);
            }
        }

        void AppendIfNotEmpty(StringBuilder stringBuilder, string propertyName, string propertyValue)
        {
            if (!string.IsNullOrEmpty(propertyValue))
            {
                stringBuilder.Append($"{propertyName}={propertyValue};");
            }
        }

        public class EdgeHubConnectionStringBuilder
        {
            readonly string hostname;
            readonly string deviceId;
            string gatewayHostname;
            string moduleId;
            string sharedAccessKey;

            public EdgeHubConnectionStringBuilder(string hostname, string deviceId)
            {
                this.hostname = hostname;
                this.deviceId = deviceId;
            }

            public EdgeHubConnectionStringBuilder SetGatewayHostName(string gatewayHostName)
            {
                this.gatewayHostname = gatewayHostName;
                return this;
            }

            public EdgeHubConnectionStringBuilder SetModuleId(string moduleIdValue)
            {
                this.moduleId = moduleIdValue;
                return this;
            }

            public EdgeHubConnectionStringBuilder SetSharedAccessKey(string sharedAccessKeyValue)
            {
                this.sharedAccessKey = sharedAccessKeyValue;
                return this;
            }

            public EdgeHubConnectionString Build()
            {
                return new EdgeHubConnectionString(this.hostname, this.gatewayHostname, this.deviceId, this.moduleId, this.sharedAccessKey);
            }
        }
    }
}
