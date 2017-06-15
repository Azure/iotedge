// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public class DirectMethodResponse
    {
        public DirectMethodResponse(string rid, byte[] data, int statusCode)
        {
            this.CorrelationId = rid;
            this.Data = data;
            this.Status = statusCode;
        }

        public byte[] Data { get; }

        public int Status { get; }

        public string CorrelationId { get; }
    }
}