// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;    

    public interface IMessage : IDisposable
    {
        IMessageSource MessageSource { get; }

        byte[] Body { get; }

        IReadOnlyDictionary<string, string> Properties { get; }

        IReadOnlyDictionary<string, string> SystemProperties { get; }

        long Offset { get; }

        DateTime EnqueuedTime { get; }

        DateTime DequeuedTime { get; }

        QueryValue GetQueryValue(string queryString);

        long Size();
    }
}
