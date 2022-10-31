// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.Azure.WebJobs.Host.Triggers;

    /// <summary>
    /// Factory used to create ITriggerBinding instances.
    /// It's TryCreateAsync method is called by the runtime for all job parameters, giving it a chance to return a binding.
    /// Please see <see href="https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Trigger-Binding-Extensions#binding-provider">Trigger Binding Extensions</see>
    /// </summary>
    public class EdgeHubTriggerBindingProvider : ITriggerBindingProvider
    {
        readonly ConcurrentDictionary<string, IList<EdgeHubMessageProcessor>> receivers = new ConcurrentDictionary<string, IList<EdgeHubMessageProcessor>>();
        ModuleClient moduleClient;
        readonly INameResolver nameResolver;

        public EdgeHubTriggerBindingProvider(INameResolver nameResolver)
        {
            this.nameResolver = nameResolver;
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

            var inputName = nameResolver?.ResolveWholeString(attribute.InputName) ?? attribute.InputName;
            inputName = inputName.ToLowerInvariant();

            await this.TrySetEventDefaultHandlerAsync();

            var messageProcessor = new EdgeHubMessageProcessor();
            var triggerBinding = new EdgeHubTriggerBinding(context.Parameter, messageProcessor);

            this.receivers.AddOrUpdate(
                inputName,
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

        async Task TrySetEventDefaultHandlerAsync()
        {
            if (this.moduleClient != null)
            {
                return;
            }

            this.moduleClient = await ModuleClientCache.Instance.GetOrCreateAsync();
            await this.moduleClient.SetMessageHandlerAsync(this.FunctionsMessageHandler, null);
        }

        async Task<MessageResponse> FunctionsMessageHandler(Message message, object userContext)
        {
            var inputName = message.InputName.ToLowerInvariant();
            byte[] payload = message.GetBytes();

            if (this.receivers.TryGetValue(inputName, out IList<EdgeHubMessageProcessor> functionReceivers))
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
