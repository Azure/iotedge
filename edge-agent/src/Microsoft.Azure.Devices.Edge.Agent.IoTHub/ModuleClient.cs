// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class ModuleClient : IModuleClient
    {
        static readonly Type[] NonRetryableExceptions =
        {
            typeof(NullReferenceException),
            typeof(ObjectDisposedException)
        };

        readonly AtomicBoolean isActive = new AtomicBoolean(true);
        readonly ISdkModuleClient inner;
        readonly ResettableTimer inactivityTimer;
        readonly ResettableTimer pingTimer;

        public ModuleClient(ISdkModuleClient inner, TimeSpan idleTimeout, bool closeOnIdleTimeout, TimeSpan connectionCheckFrequency, bool useConnectivityCheck, UpstreamProtocol protocol)
        {
            this.inner = Preconditions.CheckNotNull(inner, nameof(inner));
            this.UpstreamProtocol = protocol;
            this.inactivityTimer = new ResettableTimer(this.CloseOnInactivity, idleTimeout, Events.Log, closeOnIdleTimeout);
            this.inactivityTimer.Start();

            Events.ConnectivityCheckSetup(useConnectivityCheck, connectionCheckFrequency);
            this.pingTimer = new ResettableTimer(this.Ping, connectionCheckFrequency, Events.Log, useConnectivityCheck);
            this.pingTimer.Start();
        }

        private async Task Ping()
        {
            try
            {
                Events.PerformConnectionCheck();
                await this.UpdateReportedPropertiesAsync(new TwinCollection());
                Events.ConnectionCheckSucceeded();
            }
            catch (TimeoutException)
            {
                Events.ConnectionCheckFailed();
                await this.CloseAsync();
            }
            catch
            {
                // SDK should have thrown a TimeoutException.
                // Swallowing like we didn't send a ping check - nothing depends on the result
                Events.ConnectionCheckFailed();
            }
        }

        public event EventHandler Closed;

        public bool IsActive => this.isActive.Get();

        public UpstreamProtocol UpstreamProtocol { get; }

        public async Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged)
        {
            try
            {
                this.ResetTimters();
                await this.inner.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged);
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
        }

        public async Task SetMethodHandlerAsync(string methodName, MethodCallback callback)
        {
            try
            {
                this.ResetTimters();
                await this.inner.SetMethodHandlerAsync(methodName, callback);
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
        }

        public async Task SetDefaultMethodHandlerAsync(MethodCallback callback)
        {
            try
            {
                this.ResetTimters();
                await this.inner.SetDefaultMethodHandlerAsync(callback);
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
        }

        public async Task<Twin> GetTwinAsync()
        {
            try
            {
                this.ResetTimters();
                return await this.inner.GetTwinAsync();
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
        }

        public async Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
        {
            try
            {
                this.ResetTimters();
                await this.inner.UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
        }

        public async Task SendEventAsync(Message message)
        {
            try
            {
                this.ResetTimters();
                await this.inner.SendEventAsync(message);
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
        }

        private void ResetTimters()
        {
            // We don't reset ping timer intentionally.
            this.inactivityTimer.Reset();
        }

        ////public async Task<DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken)
        ////{
        ////    try
        ////    {
        ////        this.inactivityTimer.Reset();
        ////        return await this.inner.WaitForDeviceStreamRequestAsync(cancellationToken);
        ////    }
        ////    catch (Exception e)
        ////    {
        ////        await this.HandleException(e);
        ////        throw;
        ////    }
        ////}

        ////public async Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken)
        ////{
        ////    try
        ////    {
        ////        this.inactivityTimer.Reset();
        ////        return await this.inner.AcceptDeviceStreamingRequestAndConnect(deviceStreamRequest, cancellationToken);
        ////    }
        ////    catch (Exception e)
        ////    {
        ////        await this.HandleException(e);
        ////        throw;
        ////    }
        ////}

        public async Task CloseAsync()
        {
            try
            {
                if (this.isActive.GetAndSet(false))
                {
                    try
                    {
                        this.inactivityTimer.Dispose();
                        this.pingTimer.Dispose();

                        await this.inner.CloseAsync();
                    }
                    finally
                    {
                        // call the event even we failed: that will try to reconnect
                        this.Closed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception e)
            {
                Events.ErrorClosingClient(e);
            }
        }

        Task CloseOnInactivity()
        {
            if (this.isActive.Get())
            {
                Events.TimedOutClosing();
                return this.CloseAsync();
            }

            return Task.CompletedTask;
        }

        async Task HandleException(Exception ex)
        {
            try
            {
                if (NonRetryableExceptions.Any(e => e.IsInstanceOfType(ex)))
                {
                    Events.ClosingModuleClient(ex);
                    await this.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Events.ExceptionInHandleException(this, ex, e);
            }
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClient>();
            const int IdStart = AgentEventIds.ModuleClientProvider;

            enum EventIds
            {
                ClosingModuleClient = IdStart,
                ExceptionInHandleException,
                TimedOutClosing,
                ErrorClosingClient,
                ConnectivityCheckSetup,
                PerformConnectionCheck,
                ConnectionCheckFailed,
                ConnectionCheckSucceeded
            }

            public static void ClosingModuleClient(Exception ex)
            {
                Log.LogWarning((int)EventIds.ClosingModuleClient, ex, "Closing module client");
            }

            public static void ExceptionInHandleException(ModuleClient moduleClient, Exception ex, Exception e)
            {
                Log.LogWarning((int)EventIds.ExceptionInHandleException, $"Encountered error - {e} while trying to handle error {ex.Message}");
            }

            public static void ErrorClosingClient(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorClosingClient, ex, "Error closing module client");
            }

            public static void TimedOutClosing()
            {
                Log.LogInformation((int)EventIds.TimedOutClosing, "Edge agent module client timed out due to inactivity, closing...");
            }

            public static void ConnectivityCheckSetup(bool enabled, TimeSpan frequency)
            {
                Log.LogInformation((int)EventIds.ConnectivityCheckSetup, $"Setting up connectivity check with the following parameters - enabled: {enabled}, frequency (hh:mm:ss) {frequency}.");
            }

            public static void PerformConnectionCheck()
            {
                Log.LogInformation((int)EventIds.PerformConnectionCheck, "Performing connectivity check");
            }

            public static void ConnectionCheckFailed()
            {
                Log.LogWarning((int)EventIds.ConnectionCheckFailed, "Connection check failed");
            }

            public static void ConnectionCheckSucceeded()
            {
                Log.LogInformation((int)EventIds.ConnectionCheckSucceeded, "Connection check succeeded");
            }
        }
    }
}
