// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    /// <summary>
    /// Class to register and unregister a  messagehandler which is triggered when a new message is received
    /// </summary>
    class EdgeHubMessageProcessor
    {
        MessageHandler handler;

        public delegate Task MessageHandler(Message message, object userContext);

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
