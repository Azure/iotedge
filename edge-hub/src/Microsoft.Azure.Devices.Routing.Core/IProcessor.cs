// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;

    public interface IProcessor : ISink<IMessage>
    {
        Endpoint Endpoint { get; }

        ITransientErrorDetectionStrategy ErrorDetectionStrategy { get; }
    }
}