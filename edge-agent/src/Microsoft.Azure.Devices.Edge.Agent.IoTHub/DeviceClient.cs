// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
	using System;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Edge.Util;
	using Microsoft.Azure.Devices.Shared;
	using Microsoft.Azure.Devices.Client.Transport.Mqtt;

	public class DeviceClient : IDeviceClient
	{
		readonly Client.DeviceClient deviceClient;
		private const uint deviceClientTimeout = 30000; // ms

		public DeviceClient(string connectionString)
		{
			Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));

			// TODO: REMOVE!! -->
			var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
			mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
			// TODO: <-- REMOVE!!

			ITransportSettings[] settings = { mqttSetting };
			this.deviceClient = Client.DeviceClient.CreateFromConnectionString(connectionString, settings);
			this.deviceClient.OperationTimeoutInMilliseconds = deviceClientTimeout;
		}

		public void Dispose() => this.deviceClient.Dispose();

		public Task SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback onDesiredPropertyChanged, object userContext) =>
			this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, userContext);

		public Task<Twin> GetTwinAsync() => this.deviceClient.GetTwinAsync();

		public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

		public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler statusChangedHandler) =>
			this.deviceClient.SetConnectionStatusChangesHandler(statusChangedHandler);
	}
}