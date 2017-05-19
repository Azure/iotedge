// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public class DeviceClient : IDeviceClient
    {
        readonly Client.DeviceClient deviceClient; 

        public DeviceClient(string connectionString) :
            this(connectionString, TransportType.Mqtt)
        {
        }

        public DeviceClient(string connectionString, TransportType transportType)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            this.deviceClient = Client.DeviceClient.CreateFromConnectionString(connectionString, transportType);
        }

        public void Dispose() => this.deviceClient.Dispose();

        public Task SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback onDesiredPropertyChanged, object userContext) =>
            this.deviceClient.SetDesiredPropertyUpdateCallback(onDesiredPropertyChanged, userContext);

        public Task<Twin> GetTwinAsync() => this.deviceClient.GetTwinAsync();

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
}