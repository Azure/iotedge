// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class OfflineCheckInfo
    {
        public string Address { get; }
        public int Port { get; }

        public OfflineCheckInfo(string address, int port)
        {
            this.Address = address;
            this.Port = port;
        }
    }
}
