// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class MultipleConnectionsException : Exception
    {
        public MultipleConnectionsException(string message)
            : base(message)
        {
        }
    }
}
