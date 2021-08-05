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
            Events.ReceivedMethodCallback(methodRequest);
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

                            if (this.enableSubscriptions)
                            {
                                await this.EnableSubscriptions(mc);
                            }

                            mc.Closed += this.OnModuleClientClosed;
                            this.moduleClient = Option.Some(mc);
                            Events.InitializedNewModuleClient(this.enableSubscriptions);
                            return mc;
                        });
                return moduleClient;
            }
        }

        async Task EnableSubscriptions(IModuleClient moduleClient)
        {
            try
            {
                await moduleClient.SetDefaultMethodHandlerAsync(this.MethodCallback);
                await moduleClient.SetDesiredPropertyUpdateCallbackAsync(this.desiredPropertyUpdateCallback);
            }
            catch (Exception ex)
            {
                Events.ErrorSettingUpNewModule(ex);

                try
                {
                    await moduleClient.CloseAsync();
                }
                catch
                {
                    // swallowing intentionally
                }

                throw;
            }
        }

        async void OnModuleClientClosed(object sender, EventArgs e)
        {
            try
            {
                Events.ModuleClientClosed(this.enableSubscriptions);
                if (this.enableSubscriptions)
                {
                    await this.InitModuleClient();
                }
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingModuleClosedEvent(ex);
            }
        }

        public async void Dispose()
        {
            try
            {
                Events.DisposingModuleConnection();
                await this.moduleClient.ForEachAsync(
                    mc =>
                    {
                        mc.Closed -= this.OnModuleClientClosed;
                        return mc.CloseAsync();
                    });
                Events.DisposedModuleConnection();
            }
            catch (Exception e)
            {
                Events.ErrorDisposingModuleConnection(e);
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.ModuleClientProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClient>();

            enum EventIds
            {
                InitializedNewModuleClient = IdStart,
                ErrorHandlingModuleClosedEvent,
                ModuleClientClosed,
                ReceivedMethodCallback,
                DisposingModuleConnection,
                DisposedModuleConnection,
                ErrorDisposingModuleConnection,
                ErrorSettingUpNewModule
            }

            public static void ErrorHandlingModuleClosedEvent(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingModuleClosedEvent, ex, "Error handling module client closed event");
            }

            public static void ErrorSettingUpNewModule(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorSettingUpNewModule, ex, "Error setting up new module client");
            }

            public static void ModuleClientClosed(bool enableSubscriptions)
            {
                string message = enableSubscriptions
                    ? "Current module client closed. Initializing a new one"
                    : "Module client closed. Not initializing a new one since subscriptions are disabled.";
                Log.LogInformation((int)EventIds.ModuleClientClosed, message);
            }

            public static void InitializedNewModuleClient(bool enableSubscriptions)
            {
                string subscriptionsState = enableSubscriptions ? "enabled" : "disabled";
                Log.LogInformation((int)EventIds.InitializedNewModuleClient, $"Initialized new module client with subscriptions {subscriptionsState}");
            }

            public static void ReceivedMethodCallback(MethodRequest methodRequest)
            {
                Log.LogInformation((int)EventIds.ReceivedMethodCallback, $"Received direct method call - {methodRequest?.Name ?? string.Empty}");
            }

            public static void DisposingModuleConnection()
            {
                Log.LogInformation((int)EventIds.DisposingModuleConnection, "Disposing module connection object");
            }

            public static void DisposedModuleConnection()
            {
                Log.LogInformation((int)EventIds.DisposedModuleConnection, "Disposed module connection object");
            }

            public static void ErrorDisposingModuleConnection(Exception ex)
            {
                Log.LogInformation((int)EventIds.ErrorDisposingModuleConnection, ex, "Error disposing module connection object");
            }
        }
    }
}
