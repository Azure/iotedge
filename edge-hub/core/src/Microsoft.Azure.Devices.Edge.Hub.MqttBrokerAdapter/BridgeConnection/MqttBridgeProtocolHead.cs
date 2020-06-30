// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MqttBridgeProtocolHead : IProtocolHead
    {
        readonly MqttBridgeProtocolHeadConfig config;
        readonly IMqttBridgeConnector connector;

        public string Name => "EH-BRIDGE";

        public MqttBridgeProtocolHead(MqttBridgeProtocolHeadConfig config, IMqttBridgeConnector connector)
        {
            this.config = Preconditions.CheckNotNull(config);
            this.connector = connector;
        }

        public async Task StartAsync()
        {
            Events.Starting();

            try
            {
                await this.connector.ConnectAsync(this.config.Url, this.config.Port);
            }
            catch (Exception e)
            {
                Events.FailedToStart(e);
                throw;
            }

            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();

            try
            {
                await this.connector.DisconnectAsync();
            }
            catch (Exception e)
            {
                Events.FailedToClose(e);
                throw;
            }

            Events.Closed();
        }

        public void Dispose() => this.CloseAsync(CancellationToken.None).Wait();

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.MqttBridgeProtocolHead;
            static readonly ILogger Log = Logger.Factory.CreateLogger<MqttBridgeProtocolHead>();

            enum EventIds
            {
                Starting = IdStart,
                FailedToStart,
                Started,
                Closing,
                FailedToClose,
                Closed
            }

            public static void Starting() => Log.LogInformation((int)EventIds.Starting, "Starting EH-BRIDGE head");
            public static void FailedToStart(Exception e) => Log.LogInformation((int)EventIds.FailedToStart, e, "Failed to start EH-BRIDGE head");
            public static void Started() => Log.LogInformation((int)EventIds.Started, "Started EH-BRIDGE head");
            public static void Closing() => Log.LogInformation((int)EventIds.Closing, "Closing EH-BRIDGE head");
            public static void FailedToClose(Exception e) => Log.LogInformation((int)EventIds.FailedToClose, e, "Failed to close EH-BRIDGE head");
            public static void Closed() => Log.LogInformation((int)EventIds.Closed, "Closed EH-BRIDGE head");
        }
    }
}
