// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public interface IMessage : IDisposable
    {
        byte[] Body { get; }

        DateTime DequeuedTime { get; }

        DateTime EnqueuedTime { get; }

        IMessageSource MessageSource { get; }

        long Offset { get; }

        IReadOnlyDictionary<string, string> Properties { get; }

        IReadOnlyDictionary<string, string> SystemProperties { get; }

        QueryValue GetQueryValue(string queryString);

        long Size();
    }
}
