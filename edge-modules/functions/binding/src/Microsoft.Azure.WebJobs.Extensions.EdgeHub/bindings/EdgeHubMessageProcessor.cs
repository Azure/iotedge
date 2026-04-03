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

        public delegate Task MessageHandler(IncomingMessage message);

        public Task TriggerMessage(IncomingMessage message)
        {
            return this.handler?.Invoke(message) ?? Task.CompletedTask;
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
