namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConnectionStringBuilder
    {
        const string HostNamePropertyName = "HostName";
        const string GatewayHostNamePropertyName = "GatewayHostName";
        const string DeviceIdPropertyName = "DeviceId";
        const string ModuleIdPropertyname = "ModuleId";
        const string SharedAccessKeyPropertyName = "SharedAccessKey";
        const char ValuePairDelimiter = ';';

        readonly string iotHubHostName;
        readonly string deviceId;

        public ModuleConnectionStringBuilder(string iotHubHostName, string deviceId)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public ModuleConnectionString Create(string moduleId) => new ModuleConnectionString(this.iotHubHostName, this.deviceId, Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));

        static void AppendIfNotEmpty(StringBuilder stringBuilder, string propertyName, string propertyValue)
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

        public class ModuleConnectionString
        {
            readonly string iotHubHostName;
            readonly string deviceId;
            readonly string moduleId;
            string gatewayHostName;
            string sasKey;

            public ModuleConnectionString(string iotHubHostName, string deviceId, string moduleId)
            {
                this.iotHubHostName = iotHubHostName;
                this.deviceId = deviceId;
                this.moduleId = moduleId;
            }

            public ModuleConnectionString WithGatewayHostName(string gatewayHostName)
            {
                this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
                return this;
            }

            public ModuleConnectionString WithSharedAccessKey(string sasKey)
            {
                this.sasKey = Preconditions.CheckNonWhiteSpace(sasKey, nameof(sasKey));
                return this;
            }

            public string Build()
            {
                if (string.IsNullOrEmpty(this.moduleId))
                {
                    throw new ArgumentException("Required parameter moduleId has not been set");
                }

                var connectionString = new StringBuilder();
                AppendIfNotEmpty(connectionString, HostNamePropertyName, this.iotHubHostName);
                AppendIfNotEmpty(connectionString, DeviceIdPropertyName, this.deviceId);
                AppendIfNotEmpty(connectionString, ModuleIdPropertyname, this.moduleId);
                AppendIfNotEmpty(connectionString, SharedAccessKeyPropertyName, this.sasKey);
                AppendIfNotEmpty(connectionString, GatewayHostNamePropertyName, this.gatewayHostName);
                return connectionString.ToString();
            }

            public static implicit operator string(ModuleConnectionString moduleConnectionStringBuilder) => moduleConnectionStringBuilder.Build();
        }
    }
}
