// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class TwinNotFoundException : Exception
    {
        public TwinNotFoundException(string message)
            : this(message, null)
        {
        }

        public TwinNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
