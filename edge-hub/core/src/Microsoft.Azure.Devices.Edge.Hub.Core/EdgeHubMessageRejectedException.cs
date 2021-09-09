// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class EdgeHubMessageRejectedException : Exception
    {
        public EdgeHubMessageRejectedException(string message)
            : base(message)
        {
        }
    }
}
