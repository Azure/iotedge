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
            "Current number of connected clients to edge hub",
            EmptyStringList);

        public static readonly IMetricsCounter ClientsConnectCounter = EdgeMetrics.Instance.CreateCounter(
            "client_connect_success",
            "Total number of times each client successfully connected to edgeHub",
            new List<string>() { "id" });

        public static readonly IMetricsCounter ClientsDiscconnectCounter = EdgeMetrics.Instance.CreateCounter(
           "client_disconnect",
           "Total number of times individual client disconnected from edgeHub",
           new List<string>() { "id" });

        public static void UpdateConnectedClients(int connectedClients) => ConnectedClientsGauge.Set(connectedClients, EmptyStringArray);

        public static void OnDeviceConnected(string deviceId) => ClientsConnectCounter.Increment(1, new string[] { deviceId });

        public static void OnDeviceDisconnected(string deviceId) => ClientsDiscconnectCounter.Increment(1, new string[] { deviceId });
    }
}
