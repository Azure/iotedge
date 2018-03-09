// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This class helps interface all the different link handlers to the EdgeHub core.
    /// It provides one DeviceListener for all the link handlers. It also provides hooks
    /// for the link handlers to register callbacks, which are invoked from the DeviceProxy
    /// </summary>
    class ConnectionHandler : IConnectionHandler
    {
        Option<Func<IMessage, Task>> c2DMessageSender = Option.None<Func<IMessage, Task>>();
        Option<Func<string, IMessage, Task>> moduleMessageSender = Option.None<Func<string, IMessage, Task>>();
        Option<Func<DirectMethodRequest, Task>> methodInvoker = Option.None<Func<DirectMethodRequest, Task>>();
        Option<Func<IMessage, Task>> desiredPropertiesUpdater = Option.None<Func<IMessage, Task>>();
        bool isInitialized;
        IDeviceListener deviceListener;
        AmqpAuthentication amqpAuthentication;

        readonly AsyncLock initializationLock = new AsyncLock();
        readonly IAmqpConnection connection;
        readonly IConnectionProvider connectionProvider;

        public ConnectionHandler(IAmqpConnection connection, IConnectionProvider connectionProvider)
        {
            this.connection = Preconditions.CheckNotNull(connection, nameof(connection));
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
        }

        public async Task<IDeviceListener> GetDeviceListener()
        {
            await this.EnsureInitialized();
            return this.deviceListener;
        }

        public async Task<AmqpAuthentication> GetAmqpAuthentication()
        {
            await this.EnsureInitialized();
            return this.amqpAuthentication;
        }

        async Task EnsureInitialized()
        {
            if (!this.isInitialized)
            {
                using (await this.initializationLock.LockAsync())
                {
                    if (!this.isInitialized)
                    {
                        AmqpAuthentication amqpAuth;
                        // Check if Principal is SaslPrincipal
                        if (this.connection.Principal is SaslPrincipal saslPrincipal)
                        {
                            amqpAuth = saslPrincipal.AmqpAuthentication;
                        }
                        else
                        {
                            // Else the connection uses CBS authentication. Get AmqpAuthentication from the CbsNode                    
                            var cbsNode = this.connection.FindExtension<ICbsNode>();
                            if (cbsNode == null)
                            {
                                throw new InvalidOperationException("CbsNode is null");
                            }

                            amqpAuth = await cbsNode.GetAmqpAuthentication();
                        }

                        if (!amqpAuth.IsAuthenticated)
                        {
                            throw new InvalidOperationException("Connection not authenticated");
                        }

                        IIdentity identity = amqpAuth.Identity.Expect(() => new InvalidOperationException("Authenticated connection should have a valid identity"));
                        this.deviceListener = await this.connectionProvider.GetDeviceListenerAsync(identity);
                        var deviceProxy = new DeviceProxy(this, identity);
                        this.deviceListener.BindDeviceProxy(deviceProxy);
                        this.amqpAuthentication = amqpAuth;
                        this.isInitialized = true;
                        Events.InitializedConnectionHandler(identity);
                    }
                }
            }
        }

        public void RegisterC2DMessageSender(Func<IMessage, Task> func)
        {
            this.c2DMessageSender = Option.Some(Preconditions.CheckNotNull(func));
            Events.RegisteredC2DMessageSender(this.amqpAuthentication);
        }

        public void RegisterModuleMessageSender(Func<string, IMessage, Task> func)
        {
            this.moduleMessageSender = Option.Some(Preconditions.CheckNotNull(func));
            Events.RegisteredModuleMessageSender(this.amqpAuthentication);
        }

        public void RegisterMethodInvoker(Func<DirectMethodRequest, Task> func)
        {
            this.methodInvoker = Option.Some(Preconditions.CheckNotNull(func));
            Events.RegisteredMethodInvoker(this.amqpAuthentication);
        }

        public void RegisterDesiredPropertiesUpdateSender(Func<IMessage, Task> func)
        {
            this.desiredPropertiesUpdater = Option.Some(Preconditions.CheckNotNull(func));
            Events.RegisteredDesiredPropertiesUpdateSender(this.amqpAuthentication);
        }

        public class DeviceProxy : IDeviceProxy
        {
            readonly ConnectionHandler connectionHandler;
            bool isActive = true;

            public DeviceProxy(ConnectionHandler connectionHandler, IIdentity identity)
            {
                this.connectionHandler = connectionHandler;
                this.Identity = identity;
            }

            public Task CloseAsync(Exception ex)
            {
                Events.ClosingProxy(this.Identity, ex);
                return this.connectionHandler.connection.Close();
            }

            public Task SendC2DMessageAsync(IMessage message)
            {
                if (!this.connectionHandler.c2DMessageSender.HasValue)
                {
                    Events.NoC2DMessageHandler(this.Identity);
                }
                return this.connectionHandler.c2DMessageSender.Map(s => s.Invoke(message)).GetOrElse(Task.CompletedTask);
            }

            public Task SendMessageAsync(IMessage message, string input)
            {
                if (!this.connectionHandler.moduleMessageSender.HasValue)
                {
                    Events.NoSendMessageHandler(this.Identity);
                }
                return this.connectionHandler.moduleMessageSender.Map(s => s.Invoke(input, message)).GetOrElse(Task.CompletedTask);
            }

            public async Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request)
            {
                if (!this.connectionHandler.methodInvoker.HasValue)
                {
                    Events.NoInvokeMethodHandler(this.Identity);
                }
                await this.connectionHandler.methodInvoker.ForEachAsync(s => s.Invoke(request));
                return default(DirectMethodResponse);
            }

            public Task OnDesiredPropertyUpdates(IMessage desiredProperties)
            {
                if (!this.connectionHandler.desiredPropertiesUpdater.HasValue)
                {
                    Events.NoDesiredPropertyUpdateHandler(this.Identity);
                }
                return this.connectionHandler.desiredPropertiesUpdater.Map(s => s.Invoke(desiredProperties)).GetOrElse(Task.CompletedTask);
            }

            public bool IsActive => this.isActive;

            public IIdentity Identity { get; }

            public void SetInactive()
            {
                Events.SettingProxyInactive(this.Identity);
                this.isActive = false;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionHandler>();
            const int IdStart = AmqpEventIds.ConnectionHandler;

            enum EventIds
            {
                NoC2DMessageHandler = IdStart,
                ClosingProxy,
                NoSendMessageHandler,
                NoInvokeMethodHandler,
                NoDesiredPropertyUpdateHandler,
                SettingProxyInactive,
                RegisteredC2DMessageHandler,
                RegisteredModuleMessageHandler,
                RegisteredMethodInvoker,
                RegisteredDesiredPropertiesUpdateSender
            }

            internal static void NoC2DMessageHandler(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.NoC2DMessageHandler, $"Unable to send C2D message to {identity.Id} because no handler was registered.");
            }

            internal static void ClosingProxy(IIdentity identity, Exception ex)
            {
                Log.LogInformation((int)EventIds.ClosingProxy, ex, $"Closing AMQP device proxy for {identity.Id} because no handler was registered.");
            }

            internal static void NoSendMessageHandler(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.NoSendMessageHandler, $"Unable to send message to {identity.Id} because no handler was registered.");
            }

            internal static void NoInvokeMethodHandler(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.NoInvokeMethodHandler, $"Unable to invoke method on {identity.Id} because no handler was registered.");
            }

            internal static void NoDesiredPropertyUpdateHandler(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.NoDesiredPropertyUpdateHandler, $"Unable to send desired properties update to {identity.Id} because no handler was registered.");
            }

            internal static void SettingProxyInactive(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.SettingProxyInactive, $"Setting proxy inactive for {identity.Id}.");
            }

            internal static void RegisteredC2DMessageSender(AmqpAuthentication amqpAuthentication)
            {
                Log.LogDebug((int)EventIds.RegisteredC2DMessageHandler, $"Registered C2D message handler {amqpAuthentication?.Identity.Map(i => i.Id).GetOrElse(string.Empty) ?? string.Empty}");
            }

            internal static void RegisteredModuleMessageSender(AmqpAuthentication amqpAuthentication)
            {
                Log.LogDebug((int)EventIds.RegisteredModuleMessageHandler, $"Registered module message handler {amqpAuthentication?.Identity.Map(i => i.Id).GetOrElse(string.Empty) ?? string.Empty}");
            }

            internal static void RegisteredMethodInvoker(AmqpAuthentication amqpAuthentication)
            {
                Log.LogDebug((int)EventIds.RegisteredMethodInvoker, $"Registered method invoker {amqpAuthentication?.Identity.Map(i => i.Id).GetOrElse(string.Empty) ?? string.Empty}");
            }

            internal static void RegisteredDesiredPropertiesUpdateSender(AmqpAuthentication amqpAuthentication)
            {
                Log.LogDebug((int)EventIds.RegisteredDesiredPropertiesUpdateSender, $"Registered desired properties update sender {amqpAuthentication?.Identity.Map(i => i.Id).GetOrElse(string.Empty) ?? string.Empty}");
            }

            internal static void InitializedConnectionHandler(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.RegisteredC2DMessageHandler, $"Initialized AMQP connection handler for {identity.Id}");
            }
        }
    }
}
