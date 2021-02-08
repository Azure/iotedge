// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using EdgeMetrics = Util.Metrics.Metrics;

    public static class DeviceConnectionMetrics
    {
        static readonly List<string> EmptyStringList = new List<string>();
        public static readonly IMetricsGauge ConnectedClientsGauge = EdgeMetrics.Instance.CreateGauge(
            "connected_clients",
            "Current number of clients connected to edgeHub",
            EmptyStringList);

        public static readonly IMetricsCounter ClientsConnectCounter = EdgeMetrics.Instance.CreateCounter(
            "client_connect_success",
            "Total number of times each client successfully connected to edgeHub",
            new List<string>() { "id" });

        public static readonly IMetricsCounter ClientsDiscconnectCounter = EdgeMetrics.Instance.CreateCounter(
           "client_disconnect",
           "Total number of times each client disconnected from edgeHub",
           new List<string>() { "id" });

        public static void UpdateConnectedClients(int connectedClients) => ConnectedClientsGauge.Set(connectedClients, Array.Empty<string>());

        public static void OnDeviceConnected(string deviceId) => ClientsConnectCounter.Increment(1, new string[] { deviceId });

        public static void OnDeviceDisconnected(string deviceId) => ClientsDiscconnectCounter.Increment(1, new string[] { deviceId });
    }
}
