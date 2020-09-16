// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MqttBrokerProtocolHead : IProtocolHead
    {
        readonly MqttBrokerProtocolHeadConfig config;
        readonly IMqttBrokerConnector connector;

        int isRunning;

        public string Name => "MQTT-BROKER-HEAD";

        public MqttBrokerProtocolHead(MqttBrokerProtocolHeadConfig config, IMqttBrokerConnector connector)
        {
            this.config = Preconditions.CheckNotNull(config);
            this.connector = Preconditions.CheckNotNull(connector);
            this.isRunning = 0;
        }

        public async Task StartAsync()
        {
            Events.Starting();

            var wasRunning = Interlocked.Exchange(ref this.isRunning, 1);
            if (wasRunning == 1)
            {
                Events.WasAlreadyRunning();
                return;
            }

            try
            {
                await this.connector.ConnectAsync(this.config.Url, this.config.Port);
            }
            catch (Exception e)
            {
                Interlocked.Exchange(ref this.isRunning, 0);
                Events.FailedToStart(e);
                throw;
            }

            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            var wasRunning = Interlocked.Exchange(ref this.isRunning, 0);
            if (wasRunning == 0)
            {
                // not logging as multiple disposes or close/dispose can happen - just ignore
                return;
            }

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
            static readonly ILogger Log = Logger.Factory.CreateLogger<MqttBrokerProtocolHead>();

            enum EventIds
            {
                Starting = IdStart,
                FailedToStart,
                Started,
                WasAlreadyRunning,
                Closing,
                FailedToClose,
                Closed
            }

            public static void Starting() => Log.LogInformation((int)EventIds.Starting, "Starting MQTT-BROKER-HEAD head");
            public static void FailedToStart(Exception e) => Log.LogInformation((int)EventIds.FailedToStart, e, "Failed to start MQTT-BROKER-HEAD head");
            public static void WasAlreadyRunning() => Log.LogWarning((int)EventIds.WasAlreadyRunning, "MQTT-BROKER-HEAD was already running when started again, ignoring start attempt");
            public static void Started() => Log.LogInformation((int)EventIds.Started, "Started MQTT-BROKER-HEAD head");
            public static void Closing() => Log.LogInformation((int)EventIds.Closing, "Closing MQTT-BROKER-HEAD head");
            public static void FailedToClose(Exception e) => Log.LogInformation((int)EventIds.FailedToClose, e, "Failed to close MQTT-BROKER-HEAD head");
            public static void Closed() => Log.LogInformation((int)EventIds.Closed, "Closed MQTT-BROKER-HEAD head");
        }
    }
}
