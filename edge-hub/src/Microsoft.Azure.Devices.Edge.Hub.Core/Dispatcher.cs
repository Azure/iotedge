// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Dispatcher : IDispatcher
    {
        readonly IConnectionManager connectionManager;

        public Dispatcher(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        public Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            throw new NotImplementedException();
        }

        public async Task Dispatch(IMessage message, ISet<Endpoint> endpoints)
        {
            foreach (Endpoint endpoint in endpoints)
            {
                await this.Dispatch(message, endpoint);
            }
        }

        public async Task Dispatch(IMessage message, Endpoint endpoint)
        {
            switch (endpoint.EndpointType)
            {
                case EndpointType.Cloud:
                    Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(endpoint.DeviceId);
                    await cloudProxy.Map(cp => cp.SendMessage(message))
                        .GetOrElse(Task.FromResult(true));
                    break;

                case EndpointType.Module:
                    Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(endpoint.DeviceId);
                    await deviceProxy.Map(dp => dp.SendMessage(message))
                        .GetOrElse(TaskEx.Done);
                    break;

                case EndpointType.Null:
                    break;
            }
        }
    }
}
