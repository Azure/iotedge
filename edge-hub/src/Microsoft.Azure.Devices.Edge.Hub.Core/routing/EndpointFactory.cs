// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class EndpointFactory : IEndpointFactory
    {
        const string CloudEndpointName = "$upstream";
        const string FunctionEndpoint = "BrokeredEndpoint";
        static readonly char[] BrokeredEndpointSplitChars = { '/' };
        readonly CloudEndpoint cloudEndpoint;
        readonly IConnectionManager connectionManager;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly string edgeDeviceId;

        public EndpointFactory(IConnectionManager connectionManager,
            Core.IMessageConverter<IRoutingMessage> messageConverter,
            string edgeDeviceId)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.cloudEndpoint = new CloudEndpoint("iothub", (id) => this.connectionManager.GetCloudConnection(id), this.messageConverter);
        }

        public Endpoint CreateSystemEndpoint(string endpoint)
        {
            if (CloudEndpointName.Equals(endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return this.cloudEndpoint;
            }
            else
            {
                throw new InvalidOperationException($"System endpoint type '{endpoint ?? string.Empty}' not supported.");
            }
        }

        public Endpoint CreateFunctionEndpoint(string function, string parameterString)
        {
            if (FunctionEndpoint.Equals(function, StringComparison.OrdinalIgnoreCase))
            {
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
                string endpointId = $"{this.edgeDeviceId}/{moduleId}/{input}";
                return new ModuleEndpoint(endpointId, id, input, this.connectionManager, this.messageConverter);
            }
            else
            {
                throw new InvalidOperationException($"Function endpoint type '{function ?? string.Empty}' not supported.");
            }
        }
    }
}
