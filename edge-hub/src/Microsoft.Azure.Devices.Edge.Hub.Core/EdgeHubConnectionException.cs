// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.IO;

    public class EdgeHubConnectionException : IOException
    {
        public EdgeHubConnectionException(string message)
            : this(message, null)
        {
        }

        public EdgeHubConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
