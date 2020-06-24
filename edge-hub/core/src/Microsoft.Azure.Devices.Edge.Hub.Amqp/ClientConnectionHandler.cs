// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This class helps maintain the links on an Amqp connection, and it also acts as a common interface for all links.
    /// It maintains the IIdentity and the IDeviceListener for the connection, and provides it to the link handlers.
    /// It also maintains a registry of the links open on that connection, and makes sure duplicate/invalid links are not opened.
    /// </summary>
    class ClientConnectionHandler : IConnectionHandler
    {
        readonly IDictionary<LinkType, ILinkHandler> registry = new Dictionary<LinkType, ILinkHandler>();
        readonly IIdentity identity;

        readonly AsyncLock initializationLock = new AsyncLock();
        readonly AsyncLock registryUpdateLock = new AsyncLock();
        readonly IConnectionProvider connectionProvider;
        Option<IDeviceListener> deviceListener = Option.None<IDeviceListener>();

        public ClientConnectionHandler(IIdentity identity, IConnectionProvider connectionProvider)
        {
            this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
        }

        public Task<IDeviceListener> GetDeviceListener()
        {
            return this.deviceListener.Map(d => Task.FromResult(d))
                .GetOrElse(
                    async () =>
                    {
                        using (await this.initializationLock.LockAsync())
                        {
                            return await this.deviceListener.Map(d => Task.FromResult(d))
                                .GetOrElse(
                                    async () =>
                                    {
                                        // TODO: Implement plug and play for AMQP
                                        IDeviceListener dl = await this.connectionProvider.GetDeviceListenerAsync(this.identity, Option.None<string>());
                                        var deviceProxy = new DeviceProxy(this, this.identity);
                                        dl.BindDeviceProxy(deviceProxy);
                                        this.deviceListener = Option.Some(dl);
                                        Events.InitializedDeviceListener(this.identity);
                                        return dl;
                                    });
                        }
                    });
        }

        public async Task RegisterLinkHandler(ILinkHandler linkHandler)
        {
            using (await this.registryUpdateLock.LockAsync())
            {
                if (this.registry.TryGetValue(linkHandler.Type, out ILinkHandler currentLinkHandler))
                {
                    await currentLinkHandler.CloseAsync(Constants.DefaultTimeout);
                }

                ILinkHandler nonCorrelatedLinkHandler = null;
                switch (linkHandler.Type)
                {
                    case LinkType.MethodReceiving:
                        if (this.registry.TryGetValue(LinkType.MethodSending, out ILinkHandler methodSendingLinkHandler)
                            && methodSendingLinkHandler.CorrelationId != linkHandler.CorrelationId)
                        {
                            nonCorrelatedLinkHandler = methodSendingLinkHandler;
                        }

                        break;

                    case LinkType.MethodSending:
                        if (this.registry.TryGetValue(LinkType.MethodReceiving, out ILinkHandler methodReceivingLinkHandler)
                            && methodReceivingLinkHandler.CorrelationId != linkHandler.CorrelationId)
                        {
                            nonCorrelatedLinkHandler = methodReceivingLinkHandler;
                        }

                        break;

                    case LinkType.TwinReceiving:
                        if (this.registry.TryGetValue(LinkType.TwinSending, out ILinkHandler twinSendingLinkHandler)
                            && twinSendingLinkHandler.CorrelationId != linkHandler.CorrelationId)
                        {
                            nonCorrelatedLinkHandler = twinSendingLinkHandler;
                        }

                        break;

                    case LinkType.TwinSending:
                        if (this.registry.TryGetValue(LinkType.TwinReceiving, out ILinkHandler twinReceivingLinkHandler)
                            && twinReceivingLinkHandler.CorrelationId != linkHandler.CorrelationId)
                        {
                            nonCorrelatedLinkHandler = twinReceivingLinkHandler;
                        }

                        break;
                }

                await (nonCorrelatedLinkHandler?.CloseAsync(Constants.DefaultTimeout) ?? Task.CompletedTask);
                this.registry[linkHandler.Type] = linkHandler;
            }
        }

        public async Task RemoveLinkHandler(ILinkHandler linkHandler)
        {
            Preconditions.CheckNotNull(linkHandler);
            using (await this.registryUpdateLock.LockAsync())
            {
                if (this.registry.ContainsKey(linkHandler.Type))
                {
                    this.registry.Remove(linkHandler.Type);
                    if (this.registry.Count == 0)
                    {
                        await this.CloseConnection();
                    }
                }
            }
        }

        Task CloseAllLinks()
        {
            IList<ILinkHandler> links = this.registry.Values.ToList();
            IEnumerable<Task> closeTasks = links.Select(l => l.CloseAsync(Constants.DefaultTimeout));
            return Task.WhenAll(closeTasks);
        }

        async Task CloseConnection()
        {
            using (await this.initializationLock.LockAsync())
            {
                await this.deviceListener.ForEachAsync(d => d.CloseAsync());
            }
        }

        public class DeviceProxy : IDeviceProxy
        {
            readonly ClientConnectionHandler clientConnectionHandler;
            readonly AtomicBoolean isActive = new AtomicBoolean(true);

            public DeviceProxy(ClientConnectionHandler clientConnectionHandler, IIdentity identity)
            {
                this.clientConnectionHandler = clientConnectionHandler;
                this.Identity = identity;
            }

            public bool IsActive => this.isActive;

            public IIdentity Identity { get; }

            public Task CloseAsync(Exception ex)
            {
                if (this.isActive.GetAndSet(false))
                {
                    Events.ClosingProxy(this.Identity, ex);
                    return this.clientConnectionHandler.CloseAllLinks();
                }

                return Task.CompletedTask;
            }

            public Task SendC2DMessageAsync(IMessage message)
            {
                if (!this.clientConnectionHandler.registry.TryGetValue(LinkType.C2D, out ILinkHandler linkHandler))
                {
                    Events.LinkNotFound(LinkType.ModuleMessages, this.Identity, "C2D message");
                    return Task.CompletedTask;
                }

                message.SystemProperties[SystemProperties.To] = this.Identity is IModuleIdentity moduleIdentity
                    ? $"/devices/{HttpUtility.UrlEncode(moduleIdentity.DeviceId)}/modules/{HttpUtility.UrlEncode(moduleIdentity.ModuleId)}"
                    : $"/devices/{HttpUtility.UrlEncode(this.Identity.Id)}";
                Events.SendingC2DMessage(this.Identity);
                return ((ISendingLinkHandler)linkHandler).SendMessage(message);
            }

            public Task SendMessageAsync(IMessage message, string input)
            {
                if (!this.clientConnectionHandler.registry.TryGetValue(LinkType.ModuleMessages, out ILinkHandler linkHandler))
                {
                    Events.LinkNotFound(LinkType.ModuleMessages, this.Identity, "message");
                    return Task.CompletedTask;
                }

                message.SystemProperties[SystemProperties.InputName] = input;
                Events.SendingTelemetryMessage(this.Identity);
                return ((ISendingLinkHandler)linkHandler).SendMessage(message);
            }

            public async Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request)
            {
                if (!this.clientConnectionHandler.registry.TryGetValue(LinkType.MethodSending, out ILinkHandler linkHandler))
                {
                    Events.LinkNotFound(LinkType.ModuleMessages, this.Identity, "method request");
                    return default(DirectMethodResponse);
                }

                IMessage message = new EdgeMessage.Builder(request.Data)
                    .SetProperties(
                        new Dictionary<string, string>
                        {
                            [Constants.MessagePropertiesMethodNameKey] = request.Name
                        })
                    .SetSystemProperties(
                        new Dictionary<string, string>
                        {
                            [SystemProperties.CorrelationId] = request.CorrelationId
                        })
                    .Build();
                await ((ISendingLinkHandler)linkHandler).SendMessage(message);
                Events.SentMethodInvocation(this.Identity);
                return default(DirectMethodResponse);
            }

            public Task OnDesiredPropertyUpdates(IMessage desiredProperties)
            {
                if (!this.clientConnectionHandler.registry.TryGetValue(LinkType.TwinSending, out ILinkHandler linkHandler))
                {
                    Events.LinkNotFound(LinkType.ModuleMessages, this.Identity, "desired properties update");
                    return Task.CompletedTask;
                }

                Events.SendingDeriredPropertyUpdates(this.Identity);
                return ((ISendingLinkHandler)linkHandler).SendMessage(desiredProperties);
            }

            public Task SendTwinUpdate(IMessage twin)
            {
                if (!this.clientConnectionHandler.registry.TryGetValue(LinkType.TwinSending, out ILinkHandler linkHandler))
                {
                    Events.LinkNotFound(LinkType.ModuleMessages, this.Identity, "twin update");
                    return Task.CompletedTask;
                }

                Events.SendingTwinUpdate(this.Identity);
                return ((ISendingLinkHandler)linkHandler).SendMessage(twin);
            }

            public void SetInactive()
            {
                Events.SettingProxyInactive(this.Identity);
                this.isActive.Set(false);
            }

            public Task<Option<IClientCredentials>> GetUpdatedIdentity() => throw new NotImplementedException();
        }

        static class Events
        {
            const int IdStart = AmqpEventIds.ConnectionHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ClientConnectionHandler>();

            enum EventIds
            {
                ClosingProxy = IdStart,
                LinkNotFound,
                SettingProxyInactive,
                InitializedDeviceListener,
                SendingC2DMessage,
                SendingTelemetryMessage,
                SentMethodInvocation,
                SendingDeriredPropertyUpdates,
                SendingTwinUpdate
            }

            public static void SendingC2DMessage(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SendingC2DMessage, $"Sending C2D message to {identity.Id}");
            }

            public static void SendingTelemetryMessage(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SendingTelemetryMessage, $"Sending telemetry message to {identity.Id}");
            }

            public static void SentMethodInvocation(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SentMethodInvocation, $"Sending method invocation to {identity.Id}");
            }

            public static void SendingDeriredPropertyUpdates(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SendingDeriredPropertyUpdates, $"Sending desired properties update to {identity.Id}");
            }

            public static void SendingTwinUpdate(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SendingTwinUpdate, $"Sending twin update to {identity.Id}");
            }

            internal static void ClosingProxy(IIdentity identity, Exception ex)
            {
                Log.LogInformation((int)EventIds.ClosingProxy, ex, $"Closing AMQP device proxy for {identity.Id} because no handler was registered.");
            }

            internal static void LinkNotFound(LinkType linkType, IIdentity identity, string operation)
            {
                Log.LogWarning((int)EventIds.LinkNotFound, $"Unable to send {operation} to {identity.Id} because {linkType} link was not found.");
            }

            internal static void SettingProxyInactive(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.SettingProxyInactive, $"Setting proxy inactive for {identity.Id}.");
            }

            internal static void InitializedDeviceListener(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.InitializedDeviceListener, $"Initialized device listener in the AMQP protocol head for {identity.Id}");
            }
        }
    }
}
