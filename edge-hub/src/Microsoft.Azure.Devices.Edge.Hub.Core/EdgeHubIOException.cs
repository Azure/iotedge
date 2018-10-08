// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class EdgeHubIOException : IOException
    {
        public EdgeHubIOException(string message)
            : this(message, null)
        {
        }

        public EdgeHubIOException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
