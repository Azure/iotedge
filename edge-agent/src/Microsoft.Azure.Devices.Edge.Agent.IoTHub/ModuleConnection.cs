// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class ModuleConnection : IModuleConnection
    {
        readonly IModuleClientProvider moduleClientProvider;
        readonly AsyncLock stateLock = new AsyncLock();
        readonly IRequestManager requestManager;
        readonly ConnectionStatusChangesHandler connectionStatusChangesHandler;
        readonly DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
        readonly bool enableSubscriptions;

        Option<IModuleClient> moduleClient;

        public ModuleConnection(
            IModuleClientProvider moduleClientProvider,
            IRequestManager requestManager,
            ConnectionStatusChangesHandler connectionStatusChangesHandler,
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback,
            bool enableSubscriptions)
        {
            this.moduleClientProvider = Preconditions.CheckNotNull(moduleClientProvider, nameof(moduleClientProvider));
            this.requestManager = Preconditions.CheckNotNull(requestManager, nameof(requestManager));
            this.connectionStatusChangesHandler = Preconditions.CheckNotNull(connectionStatusChangesHandler, nameof(connectionStatusChangesHandler));
            this.desiredPropertyUpdateCallback = Preconditions.CheckNotNull(desiredPropertyUpdateCallback, nameof(desiredPropertyUpdateCallback));
            this.enableSubscriptions = enableSubscriptions;

            // Run initialize module client to create the module client. But we don't need to wait for the result.
            // The subsequent calls will automatically wait because of the lock
            Task.Run(this.InitModuleClient);
        }

        public async Task<IModuleClient> GetOrCreateModuleClient()
        {
            IModuleClient moduleClient = await this.moduleClient
                .Filter(m => m.IsActive)
                .Map(Task.FromResult)
                .GetOrElse(this.InitModuleClient);
            return moduleClient;
        }

        public Option<IModuleClient> GetModuleClient() => this.moduleClient.Filter(m => m.IsActive);

        async Task<MethodResponse> MethodCallback(MethodRequest methodRequest, object _)
        {
            (int responseStatus, Option<string> responsePayload) = await this.requestManager.ProcessRequest(methodRequest.Name, methodRequest.DataAsJson);
            return responsePayload
                .Map(r => new MethodResponse(Encoding.UTF8.GetBytes(r), responseStatus))
                .GetOrElse(() => new MethodResponse(responseStatus));
        }

        async Task<IModuleClient> InitModuleClient()
        {
            using (await this.stateLock.LockAsync())
            {
                IModuleClient moduleClient = await this.moduleClient
                    .Filter(m => m.IsActive)
                    .Map(Task.FromResult)
                    .GetOrElse(
                        async () =>
                        {
                            IModuleClient mc = await this.moduleClientProvider.Create(this.connectionStatusChangesHandler);
                            mc.Closed += this.OnModuleClientClosed;
                            if (this.enableSubscriptions)
                            {
                                await mc.SetDefaultMethodHandlerAsync(this.MethodCallback);
                                await mc.SetDesiredPropertyUpdateCallbackAsync(this.desiredPropertyUpdateCallback);
                            }

                            this.moduleClient = Option.Some(mc);
                            return mc;
                        });
                return moduleClient;
            }
        }

        async void OnModuleClientClosed(object sender, EventArgs e)
        {
            try
            {
                Events.ModuleClientClosed();
                await this.InitModuleClient();
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingModuleClosedEvent(ex);
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.ModuleClientProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClient>();

            enum EventIds
            {
                ClosingModuleClient = IdStart,
                ExceptionInHandleException,
                TimedOutClosing,
                ErrorClosingClient,
                ErrorHandlingModuleClosedEvent,
                ModuleClientClosed
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

            public static void ErrorHandlingModuleClosedEvent(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingModuleClosedEvent, ex, "Error handling module client closed event");
            }

            public static void ModuleClientClosed()
            {
                Log.LogInformation((int)EventIds.ModuleClientClosed, "Current module client closed. Initializing a new one...");
            }
        }
    }
}
