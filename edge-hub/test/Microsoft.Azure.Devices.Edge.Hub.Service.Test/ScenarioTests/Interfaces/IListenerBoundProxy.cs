// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IListenerBoundProxy
    {
        void BindListener(IDeviceListener deviceListener);
    }
}
