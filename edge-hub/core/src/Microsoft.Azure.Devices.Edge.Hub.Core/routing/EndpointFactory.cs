// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class EndpointFactory : IEndpointFactory
    {
        const string CloudEndpointName = "$upstream";
        const string FunctionEndpoint = "BrokeredEndpoint";
        static readonly char[] BrokeredEndpointSplitChars = { '/' };
        readonly IConnectionManager connectionManager;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly string edgeDeviceId;
        readonly ConcurrentDictionary<string, Endpoint> cache;
        readonly int maxBatchSize;
        readonly int upstreamFanOutFactor;
        readonly bool trackDeviceState;

        public EndpointFactory(
            IConnectionManager connectionManager,
            Core.IMessageConverter<IRoutingMessage> messageConverter,
            string edgeDeviceId,
            int maxBatchSize,
            int upstreamFanOutFactor,
            bool trackDeviceState)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.cache = new ConcurrentDictionary<string, Endpoint>();
            this.maxBatchSize = maxBatchSize;
            this.upstreamFanOutFactor = upstreamFanOutFactor;
            this.trackDeviceState = trackDeviceState;
        }

        public Endpoint CreateSystemEndpoint(string endpoint)
        {
            if (CloudEndpointName.Equals(endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return this.cache.GetOrAdd(CloudEndpointName, s => new CloudEndpoint("iothub", id => this.connectionManager.TryGetCloudConnection(id), this.messageConverter, this.trackDeviceState, this.maxBatchSize, this.upstreamFanOutFactor));
            }
            else
            {
                throw new InvalidOperationException($"System endpoint type '{endpoint ?? string.Empty}' not supported.");
            }
        }

        public Endpoint CreateFunctionEndpoint(string function, string parameterString)
        {
            if (!FunctionEndpoint.Equals(function, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Function endpoint type '{function ?? string.Empty}' not supported.");
            }

            // Parameter string contains endpoint address in this format - /modules/{mid}/inputs/{input}.
            parameterString = Preconditions.CheckNonWhiteSpace(parameterString, nameof(parameterString)).Trim();

            if (parameterString.StartsWith("/"))
            {
                parameterString = parameterString.Substring(1);
            }

            if (parameterString.EndsWith("/"))
            {
                parameterString = parameterString.Substring(0, parameterString.Length - 1);
            }

            string[] items = parameterString.Split(BrokeredEndpointSplitChars);
            if (items.Length != 4)
            {
                throw new InvalidOperationException($"Parameter string {parameterString} could not be parsed. Expect input in this format - /modules/{{mid}}/inputs/{{input}}");
            }

            string moduleId = items[1];
            string input = items[3];
            string id = $"{this.edgeDeviceId}/{moduleId}";
            string endpointId = $"{id}/{input}";
            return this.cache.GetOrAdd(endpointId, s => new ModuleEndpoint(endpointId, id, input, this.connectionManager, this.messageConverter));
        }
    }
}
