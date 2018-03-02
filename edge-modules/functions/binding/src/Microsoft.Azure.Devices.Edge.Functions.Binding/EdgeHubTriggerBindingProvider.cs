// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Functions.Binding.Bindings;
    using Microsoft.Azure.WebJobs.Host.Triggers;

    /// <summary>
    /// Factory used to create ITriggerBinding instances.
    /// It's TryCreateAsync method is called by the runtime for all job parameters, giving it a chance to return a binding.
    /// Please see <see href="https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Trigger-Binding-Extensions#binding-provider">Trigger Binding Extensions</see>
    /// </summary>
    class EdgeHubTriggerBindingProvider : ITriggerBindingProvider
    {
        readonly ConcurrentDictionary<string, IList<EdgeHubMessageProcessor>> receivers = new ConcurrentDictionary<string, IList<EdgeHubMessageProcessor>>();
        readonly string connectionString;
        readonly TransportType transportType;
        DeviceClient deviceClient;

        public EdgeHubTriggerBindingProvider(string connectionString, TransportType transportType)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.transportType = transportType;
        }

        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ParameterInfo parameter = context.Parameter;
            var attribute = parameter.GetCustomAttribute<EdgeHubTriggerAttribute>(false);
            if (attribute == null)
            {
                return null;
            }

            if (parameter.ParameterType != typeof(Message) && parameter.ParameterType != typeof(string))
            {
                throw new InvalidOperationException($"Can't bind EdgeHubTriggerAttribute to type '{parameter.ParameterType}'.");
            }

            await this.TrySetEventDefaultHandlerAsync();

            var messageProcessor = new EdgeHubMessageProcessor();
            var triggerBinding = new EdgeHubTriggerBinding(context.Parameter, messageProcessor);

            this.receivers.AddOrUpdate(
                attribute.InputName.ToLowerInvariant(),
                // The function used to generate a value for an absent.
                // Creates a new List and adds the message processor
                (k) => new List<EdgeHubMessageProcessor>()
                {
                    messageProcessor
                },
                // The function used to generate a new value for an existing key.
                // Adds the message processor to the key's existing list
                (k, v) =>
                {
                    v.Add(messageProcessor);
                    return v;
                });

            return triggerBinding;
        }

        Task TrySetEventDefaultHandlerAsync()
        {
            if (this.deviceClient != null)
            {
                return Task.CompletedTask;
            }

            this.deviceClient = DeviceClientCache.Instance.GetOrCreate(this.connectionString, this.transportType);
            return this.deviceClient.SetMessageHandlerAsync(this.FunctionsMessageHandler, null);
        }

        async Task<MessageResponse> FunctionsMessageHandler(Message message, object userContext)
        {
            byte[] payload = message.GetBytes();
            if (this.receivers.TryGetValue(message.InputName.ToLowerInvariant(), out IList<EdgeHubMessageProcessor> functionReceivers))
            {
                foreach (EdgeHubMessageProcessor edgeHubTriggerBinding in functionReceivers)
                {
                    await edgeHubTriggerBinding.TriggerMessage(Utils.GetMessageCopy(payload, message), userContext);
                }
            }

            return MessageResponse.Completed;
        }
    }
}
