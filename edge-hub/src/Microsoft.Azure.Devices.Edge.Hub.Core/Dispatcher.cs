// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class Dispatcher : IDispatcher
    {
        readonly IConnectionManager connectionManager;

        public Dispatcher(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
        }

        public Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            throw new NotImplementedException();
        }

        public async Task Dispatch(IMessage message, ISet<Endpoint> endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                await this.Dispatch(message, endpoint);
            }
        }

        public async Task Dispatch(IMessage message, Endpoint endpoint)
        {
            var connection = this.connectionManager.GetConnection(endpoint.DeviceId);
            switch (endpoint.EndpointType)
            {
                case EndpointType.Cloud:
                    await connection.CloudProxy.SendMessage(message);
                    break;
                case EndpointType.Module:
                    await connection.DeviceProxy.SendMessage(message);
                    break;
                case EndpointType.Null:
                    // To storage
                    break;
            }
        }
    }
}
