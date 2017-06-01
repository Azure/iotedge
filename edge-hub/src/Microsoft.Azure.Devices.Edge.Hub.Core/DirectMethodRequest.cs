// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public class DirectMethodRequest
    {
        public DirectMethodRequest(string id, string name, byte[] data)
        {
            this.Id = id;
            this.Name = name;
            this.Data = data;
        }

        public string Id { get; }

        public string Name { get; }

        public byte[] Data { get; }
    }
}
