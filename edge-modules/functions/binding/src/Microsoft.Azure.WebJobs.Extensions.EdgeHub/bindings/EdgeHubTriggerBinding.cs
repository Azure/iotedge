// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Devices.Client;
    using Host.Bindings;
    using Host.Executors;
    using Host.Listeners;
    using Host.Protocols;
    using Host.Triggers;

    /// <summary>
    /// Implements a trigger binding for EdgeHub which triggers a function
    /// when a message with corresponding InputName is received
    /// Please see <see href="https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Trigger-Binding-Extensions#binding">Trigger Binding Extensions</see>
    /// </summary>
    class EdgeHubTriggerBinding : ITriggerBinding
    {
        readonly ParameterInfo parameter;
        readonly EdgeHubMessageProcessor messageProcessor;
        readonly IReadOnlyDictionary<string, Type> bindingContract;

        public EdgeHubTriggerBinding(ParameterInfo parameter, EdgeHubMessageProcessor messageProcessor)
        {
            this.parameter = parameter;
            this.messageProcessor = messageProcessor;
            this.bindingContract = this.CreateBindingDataContract();
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract => this.bindingContract;

        public Type TriggerValueType => typeof(Message);

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            var triggerValue = value as Message;
            if (triggerValue == null)
            {
                throw new NotSupportedException("Message is required.");
            }

            return Task.FromResult<ITriggerData>(new TriggerData(null, this.GetBindingData(triggerValue)));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            return Task.FromResult<IListener>(new Listener(context.Executor, this.messageProcessor));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new EdgeHubTriggerParameterDescriptor
            {
                Name = this.parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "EdgeHub",
                    Description = "EdgeHub trigger fired",
                    DefaultValue = "EdgeHub"
                }
            };
        }

        IReadOnlyDictionary<string, object> GetBindingData(Message value)
        {
            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("EnqueuedTimeUtc", value.CreationTimeUtc);
            bindingData.Add("SequenceNumber", value.SequenceNumber);
            bindingData.Add("Properties", value.Properties);

            return bindingData;
        }

        IReadOnlyDictionary<string, Type> CreateBindingDataContract()
        {
            var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("EnqueuedTimeUtc", typeof(DateTime));
            contract.Add("SequenceNumber", typeof(ulong));
            contract.Add("Properties", typeof(IDictionary<string, string>));

            return contract;
        }

        class EdgeHubTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                return string.Format(CultureInfo.InvariantCulture, "EdgeHub trigger fired at {0}", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            }
        }

        class Listener : IListener
        {
            readonly ITriggeredFunctionExecutor executor;
            readonly EdgeHubMessageProcessor messageProcessor;

            public Listener(ITriggeredFunctionExecutor executor, EdgeHubMessageProcessor messageProcessor)
            {
                this.executor = executor;
                this.messageProcessor = messageProcessor;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                this.messageProcessor.SetEventDefaultHandler(this.FunctionsMessageHandler);
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                this.messageProcessor.UnsetEventDefaultHandler();
                return Task.CompletedTask;
            }

            public void Dispose() => this.messageProcessor.UnsetEventDefaultHandler();

            public void Cancel() => this.messageProcessor.UnsetEventDefaultHandler();

            Task FunctionsMessageHandler(Message message, object userContext)
            {
                var input = new TriggeredFunctionData
                {
                    TriggerValue = message,
                };
                return this.executor.TryExecuteAsync(input, CancellationToken.None);
            }
        }
    }
}
