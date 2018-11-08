// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    public interface IProcessor : ISink<IMessage>
    {
        Endpoint Endpoint { get; }

        ITransientErrorDetectionStrategy ErrorDetectionStrategy { get; }
    }
}
