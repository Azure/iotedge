// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager
{
    using System.Threading.Tasks;

    public interface IDeviceManager
    {
        Task ReprovisionDeviceAsync();
    }
}
