// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using EdgeMetrics = Util.Metrics.Metrics;

    public static class DeviceConnectionMetrics
    {
        static readonly List<string> EmptyStringList = new List<string>();
        static readonly string[] EmptyStringArray = new string[0];
        public static readonly IMetricsGauge ConnectedClientsGauge = EdgeMetrics.Instance.CreateGauge(
            "connected_clients",
            "Number of device client connected to edge hub",
            EmptyStringList);

        public static readonly IMetricsCounter ClientsConnectCounter = EdgeMetrics.Instance.CreateCounter(
            "client_connect_success",
            "Device client successfully connected to edge hub",
            new List<string>() { "id" });

        public static readonly IMetricsCounter ClientsDiscconnectCounter = EdgeMetrics.Instance.CreateCounter(
           "client_disconnect",
           "Device client disconnected from edge hub",
           new List<string>() { "id" });

        public static void UpdateConnectedClients(int connectedClients) => ConnectedClientsGauge.Set(connectedClients, EmptyStringArray);

        public static void OnDeviceConnected(string deviceId) => ClientsConnectCounter.Increment(1, new string[] { deviceId });

        public static void OnDeviceDisconnected(string deviceId) => ClientsDiscconnectCounter.Increment(1, new string[] { deviceId });
    }
}
