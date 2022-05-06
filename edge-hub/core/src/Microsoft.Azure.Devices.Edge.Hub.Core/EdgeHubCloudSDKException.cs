// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.IO;

    public class EdgeHubCloudSDKException : IOException
    {
        public EdgeHubCloudSDKException()
        {
        }

        public EdgeHubCloudSDKException(string message)
            : this(message, null)
        {
        }

        public EdgeHubCloudSDKException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
