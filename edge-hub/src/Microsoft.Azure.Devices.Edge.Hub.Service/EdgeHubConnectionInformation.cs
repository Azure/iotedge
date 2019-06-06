// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class EdgeHubConnectionInformation
    {
        public EdgeHubConnectionInformation(IConfiguration configuration)
        {
            string edgeHubConnectionString = configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);
            if (!string.IsNullOrWhiteSpace(edgeHubConnectionString))
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
                this.IotHubHostname = iotHubConnectionStringBuilder.HostName;
                this.EdgeDeviceId = iotHubConnectionStringBuilder.DeviceId;
                this.EdgeModuleId = iotHubConnectionStringBuilder.ModuleId;
                this.EdgeDeviceHostName = configuration.GetValue(Constants.ConfigKey.EdgeDeviceHostName, string.Empty);
                this.ConnectionString = Option.Some(edgeHubConnectionString);
            }
            else
            {
                this.IotHubHostname = configuration.GetValue<string>(Constants.ConfigKey.IotHubHostname);
                this.EdgeDeviceId = configuration.GetValue<string>(Constants.ConfigKey.DeviceId);
                this.EdgeModuleId = configuration.GetValue<string>(Constants.ConfigKey.ModuleId);
                this.EdgeDeviceHostName = configuration.GetValue<string>(Constants.ConfigKey.EdgeDeviceHostName);
                this.ConnectionString = Option.None<string>();
            }
        }

        public EdgeHubConnectionInformation(string iotHubHostName, string edgeDeviceId, string edgeModuleId, string edgeDeviceHostName, Option<string> connectionString)
        {
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.EdgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.EdgeModuleId = Preconditions.CheckNonWhiteSpace(edgeModuleId, nameof(edgeModuleId));
            this.EdgeDeviceHostName = Preconditions.CheckNotNull(edgeDeviceHostName, nameof(edgeDeviceHostName));
            this.ConnectionString = connectionString;
        }

        public string EdgeModuleId { get; }

        public string EdgeDeviceHostName { get; }

        public Option<string> ConnectionString { get; }

        public string EdgeDeviceId { get; }

        public string IotHubHostname { get; }
    }
}
