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

        public ModuleClient(ISdkModuleClient inner, TimeSpan idleTimeout, bool closeOnIdleTimeout, UpstreamProtocol protocol)
        {
            this.inner = Preconditions.CheckNotNull(inner, nameof(inner));
            this.UpstreamProtocol = protocol;
            this.inactivityTimer = new ResettableTimer(this.CloseOnInactivity, idleTimeout, Events.Log, closeOnIdleTimeout);
            this.inactivityTimer.Start();
        }

        public event EventHandler Closed;

        public bool IsActive => this.isActive.Get();

        public UpstreamProtocol UpstreamProtocol { get; }

        public async Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged)
        {
            try
            {
                this.inactivityTimer.Reset();
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
                this.inactivityTimer.Reset();
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
                this.inactivityTimer.Reset();
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
                this.inactivityTimer.Reset();
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
                this.inactivityTimer.Reset();
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
                this.inactivityTimer.Reset();
                await this.inner.SendEventAsync(message);
            }
            catch (Exception e)
            {
                await this.HandleException(e);
                throw;
            }
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
                    await this.inner.CloseAsync();
                    this.Closed?.Invoke(this, EventArgs.Empty);
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
                ErrorClosingClient
            }

            public static void ClosingModuleClient(Exception ex)
            {
                Log.LogWarning((int)EventIds.ClosingModuleClient, ex, "Closing module client");
            }

            public static void ExceptionInHandleException(ModuleClient moduleClient, Exception ex, Exception e)
            {
                Log.LogWarning((int)EventIds.ExceptionInHandleException, "Encountered error - {e} while trying to handle error {ex.Message}");
            }

            public static void ErrorClosingClient(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorClosingClient, ex, "Error closing module client");
            }

            public static void TimedOutClosing()
            {
                Log.LogInformation((int)EventIds.TimedOutClosing, "Edge agent module client timed out due to inactivity, closing...");
            }
        }
    }
}
