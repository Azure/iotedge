// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;

    public class EdgeAgentCloudSDKException : Exception
    {
        public EdgeAgentCloudSDKException()
        {
        }

        public EdgeAgentCloudSDKException(string message)
            : this(message, null)
        {
        }

        public EdgeAgentCloudSDKException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}