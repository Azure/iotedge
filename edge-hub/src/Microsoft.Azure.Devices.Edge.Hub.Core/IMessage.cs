// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;

    public interface IMessage : IDisposable
    {
        byte[] Body { get; }

        IDictionary<string, string> Properties { get; }

        IDictionary<string, string> SystemProperties { get; }
    }
}
