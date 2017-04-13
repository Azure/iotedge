// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Threading.Tasks;

    public interface IMqttConnectionProvider
    {
        Task Connect(string connectionString);
    }
}
