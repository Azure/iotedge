// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding.Bindings
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    /// <summary>
    /// Class to register and unregister a  messagehandler which is triggered when a new message is received
    /// </summary>
    class EdgeHubMessageProcessor
    {
        public delegate Task MessageHandler(Message message, object userContext);

        MessageHandler handler;

        public Task TriggerMessage(Message message, object userContext)
        {
            return this.handler?.Invoke(message, userContext) ?? Task.CompletedTask;
        }

        public void UnsetEventDefaultHandler()
        {
            this.handler = null;
        }

        public void SetEventDefaultHandler(MessageHandler functionsMessageHandler)
        {
            this.handler = functionsMessageHandler;
        }
    }
}